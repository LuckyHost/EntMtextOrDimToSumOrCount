
#region Namespaces


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using EntMtextOrDimToSumOrCount;
using Teigha.Colors;
using System.Runtime.ConstrainedExecution;
using System.Collections;
using Teigha.DatabaseServices.Filters;
using System.Net;









#if nanoCAD
using Application = HostMgd.ApplicationServices.Application;
using HostMgd.ApplicationServices;
using Teigha.DatabaseServices;
using HostMgd.EditorInput;
using Teigha.Geometry;
using Teigha.Runtime;
using Exception = Teigha.Runtime.Exception;
using DocumentLock = HostMgd.ApplicationServices.DocumentLock;
#else
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Exception = Autodesk.AutoCAD.Runtime.Exception;

#endif

#endregion Namespaces


namespace ent
{
    public class EntMtextOrDimToSumOrCount
    {

        private Serialize _tools;





        [CommandMethod("йф", CommandFlags.UsePickSet |
                      CommandFlags.Redraw | CommandFlags.Modal)] // название команды, вызываемой в Autocad
        public void MakeSumOrCountCommand()

        {

            //Параметры из конфига
            Config config = Config.LoadConfig();
            if (MyOpenDocument.doc == null) { return; }
            IsCheck _isCount = IsCheck.summ;

            ItemElement temp = new ItemElement();

            string IsCount = creatPromptKeywordOptions("Будем искать сумму,колличество или среднее?", new List<string> { "Сумму", "Колличество", "Среднее" }, 1);
            if (IsCount == null) { return; }

            List<string> searchElement = new List<string> { "Mtext", "Размеры" };

            if (IsCount == "Колличество")
            {
                searchElement = new List<string> { "Mtext", "Размеры", "МультиВыноски", "Прочее" };
            }


            string IsItem = creatPromptKeywordOptions("Над чем будет производить операции?", searchElement, 2);

            if (IsItem == null) { return; }


            if (IsCount == "Колличество")
            {
                _isCount = IsCheck.count;
            }

            if (IsCount == "Среднее")
            {
                _isCount = IsCheck.ave;
            }


            if (IsItem == "Mtext")
            {
                temp = getMext(_isCount);

                if (temp == null)
                {
                    return;
                }
            }

            if (IsItem == "Размеры")
            {
                temp = getDimension(_isCount);

                if (temp == null)
                {
                    return;
                }
            }

            if (IsItem == "МультиВыноски")
            {
                temp = getOther(IsCheck.mleader);

                if (temp == null)
                {
                    return;
                }
            }

            if (IsItem == "Прочее")
            {
                temp = getOther(IsCheck.other);

                if (temp == null)
                {
                    return;
                }
            }




            if (temp.AllHandel.Count() == 0)
            {
                MyOpenDocument.ed.WriteMessage("\n\nПустой выбор.\n");
                return;
            }


            PromptEntityOptions item = new PromptEntityOptions("\nВыберите объект(Mtext) куда сохранить результат: ");
            PromptEntityResult perItem = MyOpenDocument.ed.GetEntity(item);



            double offsetResult = config.defaultCoefMultiplexResult;
            if (config.isShowCoefMultiplex)
            {

                //Умножать результат на коэфицент
                PromptDoubleOptions pio = new PromptDoubleOptions("\n Коэффициент умножения результата");
                pio.AllowNegative = false;
                pio.DefaultValue = config.defaultCoefMultiplexResult;
                pio.AllowZero = false;
                PromptDoubleResult pir = MyOpenDocument.ed.GetDouble(pio);
                if (pir.Status != PromptStatus.OK)
                {
                    MyOpenDocument.ed.WriteMessage("\nВвод отменён.");
                    return;
                }
                offsetResult = pir.Value;

            }


            //Обновляем текст в Мтекст c коэф и округлением
            UpdateTextById(perItem.ObjectId, Math.Round((temp.result * offsetResult), config.roundResult).ToString(), 256);

            string xmlData = _tools.SerializeToXml<ItemElement>(temp);
            _tools.SaveXmlToXrecord(xmlData, perItem.ObjectId, "Makarov.D_entMtextOrDimensionToSum");


            if (temp.ObjSelID.Count > 0)
            {
                SelectObjects(temp.ObjSelID);
                MyOpenDocument.ed.WriteMessage("\n\n !!!!!!!!!!! \n\nЕсть фиктивные значения, я их все равно сложил, сейчас подсвечу. \n");
            }

        }

        [CommandMethod("йфф", CommandFlags.UsePickSet |
                       CommandFlags.Redraw | CommandFlags.Modal)] // название команды, вызываемой в Autocad
        public async void inDataSumm()

        {
            if (MyOpenDocument.doc == null) return;
            bool drawLeader = false;
            try
            {

                PromptEntityOptions item = new PromptEntityOptions("\n Handl. Выберите объект(Mtext) что б вернуть выделение: \n");
                PromptEntityResult perItem = MyOpenDocument.ed.GetEntity(item);


                if (perItem.Status != PromptStatus.OK)
                {
                    MyOpenDocument.ed.WriteMessage("Отмена");
                    return;

                }

                ItemElement selectionItem = _tools.ShowExtensionDictionaryContents<ItemElement>(perItem.ObjectId, "Makarov.D_entMtextOrDimensionToSum");
                if (selectionItem != null)
                {
                    MyOpenDocument.ed.WriteMessage("Работаю асинхронно, засекаю время...");
                    List<ObjectId> tempList = new List<ObjectId>();
                    Stopwatch stopwatch = new Stopwatch();


                    using (Transaction tr = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
                    {
                        stopwatch.Start();
                        await Task.Run
                        (
                                () =>
                             {

                                 foreach (string handleString in selectionItem.SerializedAllHandel)
                                 {
                                     try
                                     {
                                         long handleValue = Convert.ToInt64(handleString, 16);
                                         Handle handle = new Handle(handleValue);
                                         ObjectId objectId = MyOpenDocument.dbCurrent.GetObjectId(false, handle, 0); // Прямой вызов

                                         if (!objectId.IsNull && !objectId.IsErased)
                                         {
                                             tempList.Add(objectId);
                                         }
                                     }
                                     catch { /* Игнорируем некорректные хэндлы */ }
                                 }
                             }
                         );




                        stopwatch.Stop();
                        MyOpenDocument.ed.WriteMessage("Прошло с момента операции:  " + stopwatch.Elapsed.Milliseconds + " мc.");
                        tr.Commit();
                    }


                    //Выноска для визиуализации 
                    RXClass dimensiontClass = RXClass.GetClass(typeof(Dimension));
                    //Проверка что все объекты Mtext

                    if (tempList.All(id => id.ObjectClass.IsDerivedFrom(dimensiontClass)))
                    {


                        
                          PromptKeywordOptions itemDraw = new PromptKeywordOptions("\n Дорисовать линии к исходным данным ?");
                        itemDraw.Keywords.Add("Да");
                        itemDraw.Keywords.Add("Нет");
                        itemDraw.Keywords.Default ="Нет";
                        itemDraw.AllowNone = false;
                          PromptResult perItemDraw = MyOpenDocument.ed.GetKeywords(itemDraw);
                          if (perItemDraw.Status != PromptStatus.OK)
                          {
                              MyOpenDocument.ed.WriteMessage("Отмена");
                              return;
                          }


                          if (perItemDraw.Status == PromptStatus.OK && perItemDraw.StringResult == "Да")
                          {
                              drawLeader = true;
                          } 


                        using (Transaction tr2 = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
                        {

                            //Ранее выделенный объект 
                            DBObject obj = tr2.GetObject(perItem.ObjectId, OpenMode.ForRead);


                            BlockTable bt = tr2.GetObject(MyOpenDocument.dbCurrent.BlockTableId, OpenMode.ForRead) as BlockTable;
                            BlockTableRecord modelSpace = tr2.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;


                            if (drawLeader && obj is MText)
                            {
                                MText mText = obj as MText;

                                string currentLayer = tr2.GetObject(MyOpenDocument.dbCurrent.Clayer, OpenMode.ForRead) is LayerTableRecord currLtr ? currLtr.Name : "0";

                                // --- ШАГ 1: Создаем и настраиваем НОВУЮ мультивыноску с нуля ---
                                MLeader mLeader = new MLeader();
                                mLeader.SetDatabaseDefaults(MyOpenDocument.dbCurrent);
                                mLeader.MLeaderStyle = MyOpenDocument.dbCurrent.MLeaderstyle;
                                mLeader.ContentType = ContentType.MTextContent;

                                // Устанавливаем текст и его положение. Текст пустой, как в вашем примере.
                                MText newMtext = new MText() { Contents = "" };
                                mLeader.MText = newMtext;
                                mLeader.TextLocation = mText.Location; // Положение текста берем из исходного MText
                                mLeader.Layer = currentLayer;


                                // --- ШАГ 2: Добавляем ее в чертеж ОДИН РАЗ ---
                                modelSpace.AppendEntity(mLeader);
                                tr2.AddNewlyCreatedDBObject(mLeader, true);

                                int leaderIndex = mLeader.AddLeader();

                                // Проходим по каждому ObjectId из списка
                                foreach (ObjectId dimensionId in tempList)
                                {
                                    // Открываем объект как Dimension
                                    Dimension dim = tr2.GetObject(dimensionId, OpenMode.ForRead) as Dimension;

                                    // Пропускаем, если объект не является размером или был удален
                                    if (dim == null) continue;

                                    // Получаем центральную точку текста размера
                                    Point3d dimensionTextPosition = dim.TextPosition;

                                    // Добавляем новую линию в наш кластер и задаем положение ее стрелки
                                    int newLeaderLineIndex = mLeader.AddLeaderLine(leaderIndex);

                                    //mLeader.AddFirstVertex(newLeaderLineIndex, mText.Location);
                                    mLeader.AddFirstVertex(newLeaderLineIndex, dimensionTextPosition);
                                    mLeader.AddLastVertex(newLeaderLineIndex, mText.Location);
                                }


                            }
                            tr2.Commit();
                        }
                    }



                    if (tempList.Count < selectionItem.SerializedAllHandel.Count)
                    {
                        MyOpenDocument.ed.WriteMessage($"\nВНИМАНИЕ: Найдено {tempList.Count} из {selectionItem.SerializedAllHandel.Count} объектов. Возможно, часть из них была удалена.");
                    }
                    else
                    {
                        MyOpenDocument.ed.WriteMessage($"\nНайдено {tempList.Count} объектов. Все на месте!");
                    }

                    //Выделить все
                    SelectObjects(tempList);
                }


            }
            catch (Exception ex)
            {
                MyOpenDocument.ed.WriteMessage(ex.ToString());
                return;
            }

        }






        [CommandMethod("цф", CommandFlags.UsePickSet |
                      CommandFlags.Redraw | CommandFlags.Modal)] // название команды, вызываемой в Autocad
        public void DecomposePL()

        {
            List<Point2d> listPoints = new List<Point2d>();
            Layer.deleteObjectsOnLayer("Высоты_Makarov.D", false);
            Layer.creatLayer("Высоты_Makarov.D", 179, 37, 0);


            Double disZ = 0;
            if (MyOpenDocument.doc == null) return;

            PromptEntityOptions item = new PromptEntityOptions("\n Выберите полилинию для разложения: \n");
            PromptEntityResult perItem = MyOpenDocument.ed.GetEntity(item);
            if (perItem.Status != PromptStatus.OK)
            {
                MyOpenDocument.ed.WriteMessage("Отмена");
                return;
            }
            // Начинаем транзакцию
            using (Transaction tr = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
            {
                // Добавляем новую полилинию в чертеж
                BlockTable bt = tr.GetObject(MyOpenDocument.dbCurrent.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;


                if (perItem != null)
                {
                    // Открываем объект для чтения
                    Entity ent = tr.GetObject(perItem.ObjectId, OpenMode.ForRead) as Entity;

                    if (ent != null)
                    {
                        // Проверяем, является ли объект полилинией (2D или 3D)
                        if (ent is Polyline)
                        {
                            Polyline polyline = ent as Polyline;
                            MyOpenDocument.ed.WriteMessage($"\nВыбрана полилиния с {polyline.NumberOfVertices} вершинами.");

                            // Создаем новую полилинию для прямого пути
                            Polyline newPolyline = new Polyline();
                            double currentX = polyline.GetPoint2dAt(0).X; // Начальная координата X для новой полилинии
                            double currentY = polyline.GetPoint2dAt(0).Y; // Начальная координата X для новой полилинии
                            int index = 0;

                            // Проходим по каждому сегменту полилинии
                            for (int i = 0; i < polyline.NumberOfVertices - 1; i++)
                            {
                                //Филтр полилиний и проверка на замкнутость
                                SelectionFilter acSF = new SelectionFilter
                                    (
                                    new TypedValue[]
                                        { new TypedValue((int)DxfCode.Start, "POINT")
                                        }

                                    );


                                // Создаем многоугольник, приближающий окружность с центром в текущей вершине и радиусом searchDistance
                                Point3dCollection polygonPoints = createCirclePolygon(new Point3d(polyline.GetPoint2dAt(i).X, polyline.GetPoint2dAt(i).Y, 0), 0.2, 26);

                                //Тут Полигон
                                PromptSelectionResult acPSR = MyOpenDocument.ed.SelectCrossingPolygon(polygonPoints, acSF);

                                if (acPSR.Status == PromptStatus.OK)
                                {
                                    // Пройдите по найденным объектам
                                    foreach (SelectedObject acSObj in acPSR.Value)
                                    {
                                        DBPoint point = tr.GetObject(acSObj.ObjectId, OpenMode.ForWrite) as DBPoint;
                                        disZ = point.Position.Z;
                                        //MyOpenDocument.ed.WriteMessage($"\nВыбран объект является точкой с координатами: X={point.Position.X}, Y={point.Position.Y}, Z={point.Position.Z}");
                                        // MyOpenDocument.ed.WriteMessage(disZ.ToString());

                                    }

                                }
                                else { MyOpenDocument.ed.WriteMessage("ГДЕ-ТО Я не нашел рядом точки!"); return; }

                                // Получаем две точки: текущую и следующую вершины
                                Point3d pt1 = polyline.GetPoint3dAt(i);
                                Point3d pt2 = polyline.GetPoint3dAt(i + 1);

                                // Вычисляем длину сегмента как расстояние между точками
                                double segmentLength = pt1.DistanceTo(pt2);
                                //ed.WriteMessage($"\nДлина сегмента {i + 1}: {segmentLength}");

                                //Создать текст номера сегмента на самой линии
                                Text.creatText("Высоты_Makarov.D", new Point2d(polyline.GetPoint3dAt(i).X + segmentLength / 2, polyline.GetPoint3dAt(i).Y), (index + 1).ToString(), "1", 54, 0);


                                //Строим Pl высоты 
                                Polyline ZPolyline = new Polyline();
                                ZPolyline.AddVertexAt(0, new Point2d(currentX, currentY), 0, 0, 0);
                                ZPolyline.AddVertexAt(1, new Point2d(currentX, currentY + disZ), 0, 0, 0);

                                //Добавляет в ЛистПоинтв
                                listPoints.Add(new Point2d(currentX, currentY + disZ));

                                //Создать текст высот
                                Text.creatText("Высоты_Makarov.D", new Point2d(currentX, currentY + disZ), Math.Round(disZ, 2).ToString(), "1", 256, 2);
                                Text.creatText("Высоты_Makarov.D", new Point2d(currentX + segmentLength / 2, currentY), Math.Round(segmentLength, 1).ToString(), "1", 171, 1);

                                //MyOpenDocument.ed.WriteMessage(currentX.ToString(), currentY.ToString());
                                btr.AppendEntity(ZPolyline);
                                tr.AddNewlyCreatedDBObject(ZPolyline, true);


                                //Создать текст номера сегмента
                                Text.creatText("Высоты_Makarov.D", new Point2d(currentX + segmentLength / 2, currentY), (index + 1).ToString(), "1", 256, 2);

                                // Добавляем вершину в новую полилинию Сама развертка
                                newPolyline.AddVertexAt(index, new Point2d(currentX, currentY), 0, 0, 0);
                                currentX += segmentLength;
                                index++;
                            }

                            // Обработка последнего сегмента, если полилиния замкнута
                            if (polyline.Closed)
                            {
                                Point3d pt1 = polyline.GetPoint3dAt(polyline.NumberOfVertices - 1);
                                Point3d pt2 = polyline.GetPoint3dAt(0);
                                double segmentLength = pt1.DistanceTo(pt2);
                                MyOpenDocument.ed.WriteMessage($"\nДлина последнего сегмента: {segmentLength}");
                            }


                            btr.AppendEntity(newPolyline);
                            tr.AddNewlyCreatedDBObject(newPolyline, true);

                            //Строим PL ПО вершинам
                            Draw.drawPolyline(listPoints, "Высоты_Makarov.D", 21, 0.5);
                            MyOpenDocument.ed.WriteMessage($"\nНовая полилиния построена с {newPolyline.NumberOfVertices} вершинами.");


                        }
                        else if (ent is Polyline2d)
                        {
                            Polyline2d polyline2d = ent as Polyline2d;
                            MyOpenDocument.ed.WriteMessage("\nВыбрана 2D полилиния.");
                        }
                        else
                        {
                            MyOpenDocument.ed.WriteMessage("\nВыбранный объект не является полилинией.");
                        }
                    }
                }


                // Завершаем транзакцию
                tr.Commit();

            }

        }



        [CommandMethod("цфф", CommandFlags.UsePickSet |
                     CommandFlags.Redraw | CommandFlags.Modal)] // название команды, вызываемой в Autocadw
        public void FindZBetweenTwoPoints()
        {

            // Сохраняем текущее состояние привязок
            object currentOsnapModes = Application.GetSystemVariable("OSMODE");


            try
            {
                // Устанавливаем привязку "К узлу" (Node Snap). В AutoCAD она имеет значение 8
                Application.SetSystemVariable("OSMODE", 8);

                MyOpenDocument.ed.WriteMessage("\nПривязка к узлу включена. Выполните необходимые действия.");





                // Запрашиваем у пользователя ввод первой точки
                PromptPointOptions ppo1 = new PromptPointOptions("\nУкажите первую точку:");
                PromptPointResult ppr1 = MyOpenDocument.ed.GetPoint(ppo1);

                if (ppr1.Status != PromptStatus.OK)
                {
                    MyOpenDocument.ed.WriteMessage("\nПервая точка не была выбрана.");
                    return;
                }

                Point3d point1 = ppr1.Value;

                // Запрашиваем у пользователя ввод второй точки
                PromptPointOptions ppo2 = new PromptPointOptions("\nУкажите вторую точку:");
                PromptPointResult ppr2 = MyOpenDocument.ed.GetPoint(ppo2);

                if (ppr2.Status != PromptStatus.OK)
                {
                    MyOpenDocument.ed.WriteMessage("\nВторая точка не была выбрана.");
                    return;
                }

                Point3d point2 = ppr2.Value;

                //Восстанавлтиваем привязки
                Application.SetSystemVariable("OSMODE", currentOsnapModes);


                /*List<Point2d> listPoints = new List<Point2d>();
                listPoints.Add(new Point2d(point1.X, point1.Y));
                listPoints.Add(new Point2d(point2.X, point2.Y));*/

                List<Point3d> listPoints = new List<Point3d>();

                listPoints.Add(point1);
                listPoints.Add(point2);

                Layer.creatLayer("Высоты_Makarov.D", 179, 37, 0);
                //ObjectId objID = Draw.drawPolyline(listPoints, "Высоты_Makarov.D", 21, 0.08);
                ObjectId objID = Draw.DrawPolyline3d(listPoints, "Высоты_Makarov.D", 21);

                // Запрашиваем у пользователя ввод целевой точки
                PromptPointOptions ppoTarget = new PromptPointOptions("\nУкажите целевую точку:");
                PromptPointResult pprTarget = MyOpenDocument.ed.GetPoint(ppoTarget);

                if (pprTarget.Status != PromptStatus.OK)
                {
                    MyOpenDocument.ed.WriteMessage("\nЦелевая точка не была выбрана.");
                    return;
                }

                Point3d targetPoint = pprTarget.Value;

                // Вычисляем значение Z для целевой точки с помощью линейной интерполяции
                double Ztarget = LinearInterpolateZ(point1, point2, targetPoint);


                Draw.сreatePoint(new Point3d(targetPoint.X, targetPoint.Y, Ztarget), "Высоты_Makarov.D"); ;
                Draw.deleteObject(objID);

                Text.creatText("Высоты_Makarov.D", new Point2d(targetPoint.X, targetPoint.Y), (Math.Round(Ztarget, 2)).ToString(), "1", 256, 0);


                // Выводим результат
                MyOpenDocument.ed.WriteMessage($"\nЗначение Z в целевой точке ({targetPoint.X}, {targetPoint.Y}): {Ztarget}");

            }
            finally
            {     // Восстанавливаем предыдущие привязки
                Application.SetSystemVariable("OSMODE", currentOsnapModes);
                MyOpenDocument.ed.WriteMessage("\nВсе привязки восстановлены.");
            }

        }

        // Метод для линейной интерполяции
        public double LinearInterpolateZ(Point3d point1, Point3d point2, Point3d targetPoint)
        {
            double X1 = point1.X, Y1 = point1.Y, Z1 = point1.Z;
            double X2 = point2.X, Y2 = point2.Y, Z2 = point2.Z;

            // Вычисляем полное расстояние между двумя точками
            double dTotal = new Point3d(X1, Y1, 0).DistanceTo(new Point3d(X2, Y2, 0));

            // double dTotal = point1.DistanceTo(point2);

            //ТУТ НАДО БЕЗ Z Иначе типо неправильно считает, хотя правильно
            // Вычисляем расстояние от первой точки до целевой точки
            double dTarget = new Point3d(X1, Y1, 0).DistanceTo(new Point3d(targetPoint.X, targetPoint.Y, 0));

            // Линейная интерполяция
            double Ztarget = Z1 + (dTarget / dTotal) * (Z2 - Z1);
            // MyOpenDocument.ed.WriteMessage("Z1 " +Z1+ " dTarget "+ dTarget+ " dTotal " + dTotal + " Z2 "+Z2+ " Z1 "+ Z1 + "      "+Ztarget);

            return Ztarget;
        }


        private ItemElement GetDimension(IsCheck isCheck)
        {
            ItemElement resultItem = new ItemElement();
            List<Handle> tempListHandle = new List<Handle>();
            List<ObjectId> tempObjectID = new List<ObjectId>();

            PromptSelectionResult acSSPrompt = MyOpenDocument.ed.GetSelection();

            if (acSSPrompt.Status == PromptStatus.OK)
            {
                SelectionSet acSSet = acSSPrompt.Value;

                foreach (SelectedObject acSSObj in acSSet)
                {
                    if (acSSObj != null)
                    {
                        using (Transaction trAdding = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
                        {
                            ObjectId objId = acSSObj.ObjectId;

                            // Проверка на dim
                            if (trAdding.GetObject(objId, OpenMode.ForRead) is Dimension entity)
                            {
                                tempListHandle.Add(entity.ObjectId.Handle);
                                tempObjectID.Add(entity.ObjectId);

                                bool isHandresultMeasurement = string.IsNullOrEmpty(entity.DimensionText); // Если вручную текст вбит

                                if (!isHandresultMeasurement)
                                {
                                    double doleValue = 0;
                                    bool isAdd = double.TryParse(entity.DimensionText.Trim().Replace(",", "."), out doleValue);

                                    if (isAdd) // Проверка, можно ли преобразовать в число
                                    {
                                        resultItem.ObjSelID.Add(objId);
                                        resultItem.result += doleValue;
                                    }
                                    else
                                    {
                                        trAdding.Commit();
                                        ZoomToEntity(objId, 10);
                                        MyOpenDocument.ed.WriteMessage("\n\n Ты где-то ошибся, есть нечисловой текст \n Перепроверь, я тут подожду.");
                                        return null; // Здесь всё нормально, возврат null при ошибке
                                    }
                                }
                                else
                                {
                                    double resultMeasurement = Math.Round(entity.Measurement, entity.Dimdec); // Измеренное значение с округлением
                                    resultItem.result += resultMeasurement;
                                }

                                trAdding.Commit();
                            }
                        }
                    }
                }

                resultItem.AllHandel = tempListHandle;
                resultItem.AllObjectID = new List<ObjectId>(tempObjectID);

                if (isCheck == IsCheck.count)
                {
                    resultItem.result = resultItem.AllHandel.Count();
                }
                else if (isCheck == IsCheck.ave && resultItem.AllHandel.Count > 0) // Добавлена проверка на ноль
                {
                    resultItem.result /= resultItem.AllHandel.Count();
                }
            }

            return resultItem; // Возврат по умолчанию вне условия
        }



        [CommandMethod("цффф")]
        public void AddDimensionsUniversal()
        {
            Document doc = MyOpenDocument.doc;
            Database db = MyOpenDocument.dbCurrent;
            Editor ed = MyOpenDocument.ed;

            // --- Регенерация экрана ---
            // ed.Regen();

            // Выбор режима работы
            PromptKeywordOptions modeOptions = new PromptKeywordOptions("\nВыберите режим:");
            modeOptions.Keywords.Add("Сегменты");
            modeOptions.Keywords.Add("Подсегменты");
            modeOptions.Keywords.Add("ВложенныеПолилинии");
            modeOptions.Keywords.Default = "Сегменты";

            PromptResult modeResult = ed.GetKeywords(modeOptions);
            if (modeResult.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nВвод отменён.");
                return;
            }

            string mode = modeResult.StringResult;

            // Запрос отступа для размеров
            PromptDoubleOptions offsetOptions = new PromptDoubleOptions("\nУкажите отступ размера от сегмента:");
            offsetOptions.AllowNegative = false;
            offsetOptions.DefaultValue = 1.5;

            PromptDoubleResult offsetResult = ed.GetDouble(offsetOptions);
            if (offsetResult.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nВвод отменён.");
                return;
            }
            double offsetText = offsetResult.Value;

            // Выбираем основную полилинию
            PromptEntityOptions peo = new PromptEntityOptions("\nВыберите основную полилинию:");
            peo.SetRejectMessage("\nВыберите полилинию.");
            peo.AddAllowedClass(typeof(Polyline), true);

            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nВыбор отменён.");
                return;
            }

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Polyline mainPolyline = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;
                if (mainPolyline == null)
                {
                    ed.WriteMessage("\nОшибка чтения полилинии.");
                    return;
                }

                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                // Получаем имя текущего слоя
                string currentLayerName = db.Clayer.GetObject(OpenMode.ForRead) is LayerTableRecord currentLayer
                    ? currentLayer.Name
                    : "0"; // если не удалось получить - слой "0"



                // 1. Создание пустого списка для хранения найденных полилиний.
                List<Polyline> allPolylines = new List<Polyline>();

                // 2. Условие: код выполняется, только если выбран один из двух режимов.
                if (mode == "Подсегменты" || mode == "ВложенныеПолилинии")
                {
                    // 3. Получаем доступ к таблице слоев чертежа для последующей проверки.
                    LayerTable layerTable = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;


                    //Создаем фильтр, чтобы выбирать ТОЛЬКО полилинии

                    //Филтр полилиний и проверка на замкнутость
                    TypedValue[] filterValues = new TypedValue[]
                        {
                        // --- Начало логической группы "ИЛИ" ---
                        new TypedValue((int)DxfCode.Operator, "<OR"), 

                        // Правило 1: Тип объекта - "POLYLINE" (старая тяжелая 3D полилиния)
                        new TypedValue((int)DxfCode.Start, "POLYLINE"), 
    
                        // Правило 2: Тип объекта - "LWPOLYLINE" (легковесная 2D полилиния)
                        new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),

                        // --- Конец логической группы "ИЛИ" ---
                        new TypedValue((int)DxfCode.Operator, "OR>")
                        };

                    SelectionFilter filter = new SelectionFilter(filterValues);

                    // 2. Просим редактор выбрать все объекты, соответствующие фильтру
                    PromptSelectionResult psr = ed.SelectAll(filter);

                    // 3.Работаем с уже отфильтрованным, гораздо меньшим набором
                    if (psr.Status == PromptStatus.OK)
                    {
                        foreach (ObjectId id in psr.Value.GetObjectIds())
                        {
                            if (id == mainPolyline.ObjectId) continue;

                            // Открываем полилинию напрямую, т.к. фильтр гарантирует тип
                            var pl = tr.GetObject(id, OpenMode.ForRead) as Polyline;

                            // Дополнительная проверка на случай, если что-то пошло не так (опционально, но рекомендуется)
                            if (pl == null) continue;

                            // Проводим проверки на видимость
                            if (layerTable.Has(pl.Layer))
                            {
                                LayerTableRecord ltr = tr.GetObject(layerTable[pl.Layer], OpenMode.ForRead) as LayerTableRecord;
                                if (!ltr.IsOff && !ltr.IsFrozen && pl.Visible)
                                {
                                    allPolylines.Add(pl);
                                }
                            }
                        }
                    }

                }



                for (int i = 0; i < mainPolyline.NumberOfVertices - (mainPolyline.Closed ? 0 : 1); i++)
                {
                    Point3d startPoint = mainPolyline.GetPoint3dAt(i);
                    Point3d endPoint = (i == mainPolyline.NumberOfVertices - 1) ? mainPolyline.GetPoint3dAt(0) : mainPolyline.GetPoint3dAt(i + 1);

                    Line segmentLine = new Line(startPoint, endPoint);

                    List<Point3d> splitPoints = new List<Point3d> { startPoint, endPoint };

                    if (mode == "Подсегменты" || mode == "ВложенныеПолилинии")
                    {
                        foreach (Polyline poly in allPolylines)
                        {
                            // Для режима "Подсегменты" - только пересечения
                            if (mode == "Подсегменты")
                            {
                                Point3dCollection intersections = new Point3dCollection();
                                poly.IntersectWith(segmentLine, Intersect.OnBothOperands, intersections, IntPtr.Zero, IntPtr.Zero);

                                foreach (Point3d p in intersections)
                                {
                                    if (!splitPoints.Contains(p))
                                        splitPoints.Add(p);
                                }
                            }
                            // Для режима "ВложенныеПолилинии" - и пересечения, и вложенные целиком
                            else if (mode == "ВложенныеПолилинии")
                            {
                                Point3dCollection intersections = new Point3dCollection();
                                poly.IntersectWith(segmentLine, Intersect.OnBothOperands, intersections, IntPtr.Zero, IntPtr.Zero);

                                foreach (Point3d p in intersections)
                                {
                                    if (!splitPoints.Contains(p))
                                        splitPoints.Add(p);
                                }

                                if (IsPolylineInsideSegment(poly, segmentLine))
                                {
                                    if (!splitPoints.Contains(poly.StartPoint)) splitPoints.Add(poly.StartPoint);
                                    if (!splitPoints.Contains(poly.EndPoint)) splitPoints.Add(poly.EndPoint);
                                }
                            }
                        }
                    }

                    // Сортируем точки вдоль сегмента
                    splitPoints = splitPoints.OrderBy(p => (p - startPoint).Length).ToList();

                    // Создаём размеры между точками
                    for (int j = 0; j < splitPoints.Count - 1; j++)
                    {
                        Point3d p1 = splitPoints[j];
                        Point3d p2 = splitPoints[j + 1];

                        using (AlignedDimension dim = new AlignedDimension())
                        {
                            dim.XLine1Point = p1;
                            dim.XLine2Point = p2;
                            dim.DimLinePoint = CalculateDimLinePoint(p1, p2, offsetText);
                            dim.DimensionStyle = db.Dimstyle;

                            // >>> Задаём слой создания размера <<<
                            dim.Layer = currentLayerName;


                            btr.AppendEntity(dim);
                            tr.AddNewlyCreatedDBObject(dim, true);
                        }
                    }
                }

                tr.Commit();
            }

            ed.WriteMessage("\nРазмеры успешно добавлены.");
        }

        // Проверка на вложенность полилинии внутри отрезка
        private bool IsPolylineInsideSegment(Polyline poly, Line segmentLine)
        {
            Line3d line3d = new Line3d(segmentLine.StartPoint, segmentLine.EndPoint);

            for (int i = 0; i < poly.NumberOfVertices; i++)
            {
                Point3d pt = poly.GetPoint3dAt(i);
                double param = line3d.GetParameterOf(pt);
                if (param < 0 || param > 1)
                    return false;

                double distance = line3d.GetDistanceTo(pt);
                if (distance > Tolerance.Global.EqualPoint)
                    return false;
            }
            return true;
        }

        // Вычисление точки размещения размера
        private Point3d CalculateDimLinePoint(Point3d p1, Point3d p2, double offset)
        {
            Vector3d dir = (p2 - p1).GetPerpendicularVector().GetNormal();
            return new Point3d((p1.X + p2.X) / 2 + dir.X * offset, (p1.Y + p2.Y) / 2 + dir.Y * offset, 0);
        }

        [CommandMethod("gnb")]
        public void gnb()
        {

            MyOpenDocument.ed.WriteMessage("\nПостроенеи профиля ГНБ по методике \" СП 42 - 101 - 2003 Общие положения по проектированию и строительству газораспределительных систем из металлических и полиэтиленовых труб \" " );

            MyOpenDocument.ed.WriteMessage("\n!ВНИМАНИЕ. Построение выполнять при масштабе 1:1 ");



            // Выбираем точку входа ГНБ
            PromptEntityOptions peoCircleIn = new PromptEntityOptions("\nВыберите точку входа ГНБ :");
            peoCircleIn.SetRejectMessage("\nВыберите окружность");
            peoCircleIn.AddAllowedClass(typeof(Circle), true);

            PromptEntityResult perCircleIn = MyOpenDocument.ed.GetEntity(peoCircleIn);
            if (perCircleIn.Status != PromptStatus.OK)
            {
                MyOpenDocument.ed.WriteMessage("\nВыбор отменён.");
                return;
            }

            // Выбираем угол входа ГНБ
            PromptDoubleOptions pdoCircleInAngel = new PromptDoubleOptions("\nВведите угол входа точка входа в десятичных градусах:");
            //Угол в радианах
            pdoCircleInAngel.DefaultValue = 17.5;
            pdoCircleInAngel.AllowNegative = false;
            pdoCircleInAngel.AllowZero = false;

            PromptDoubleResult pdrCircleInAngel = MyOpenDocument.ed.GetDouble(pdoCircleInAngel);

            if (pdrCircleInAngel.Status != PromptStatus.OK)
            {
                MyOpenDocument.ed.WriteMessage("\nВвод угла отменен.");
                return;
            }

            //Нижняя точка
            PromptEntityOptions peoCircleIn2 = new PromptEntityOptions("\nВыберите нижнюю точку входа ГНБ :");
            peoCircleIn2.SetRejectMessage("\nВыберите окружность");
            peoCircleIn2.AddAllowedClass(typeof(Circle), true);

            PromptEntityResult perCircleIn2 = MyOpenDocument.ed.GetEntity(peoCircleIn2);
            if (perCircleIn2.Status != PromptStatus.OK)
            {
                MyOpenDocument.ed.WriteMessage("\nВыбор отменён.");
                return;
            }



            //Выход

            // Выбираем точку входа ГНБ
            PromptEntityOptions peoCircleOut = new PromptEntityOptions("\nВыберите точку выхода ГНБ :");
            peoCircleOut.SetRejectMessage("\nВыберите окружность");
            peoCircleOut.AddAllowedClass(typeof(Circle), true);

            PromptEntityResult perCircleOut = MyOpenDocument.ed.GetEntity(peoCircleOut);
            if (perCircleOut.Status != PromptStatus.OK)
            {
                MyOpenDocument.ed.WriteMessage("\nВыбор отменён.");
                return;
            }


            // Выбираем угол выхода ГНБ
            PromptDoubleOptions pdoCircleOutAngel = new PromptDoubleOptions("\nВведите угол выхода точка входа в десятичных градусах:");
            //Угол в радианах
            pdoCircleOutAngel.DefaultValue = 17.5;
            pdoCircleOutAngel.AllowNegative = false;
            pdoCircleOutAngel.AllowZero = false;

            PromptDoubleResult pdrCircleOutAngel = MyOpenDocument.ed.GetDouble(pdoCircleOutAngel);

            if (pdrCircleOutAngel.Status != PromptStatus.OK)
            {
                MyOpenDocument.ed.WriteMessage("\nВвод угла отменен.");
                return;
            }

            //Нижняя точка
            PromptEntityOptions peoCircleOut2 = new PromptEntityOptions("\nВыберите нижнюю точку выхода ГНБ :");
            peoCircleIn2.SetRejectMessage("\nВыберите окружность");
            peoCircleIn2.AddAllowedClass(typeof(Circle), true);

            PromptEntityResult perCircleOut2 = MyOpenDocument.ed.GetEntity(peoCircleOut2);
            if (perCircleIn2.Status != PromptStatus.OK)
            {
                MyOpenDocument.ed.WriteMessage("\nВыбор отменён.");
                return;
            }

            
            //

            //Вычисления радиуса обозначение из СП
            double D1;
            double D2;
            bool isRevers = false;
            using (var tr = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
            {
            //Для входа
                Circle CircleIn = tr.GetObject(perCircleIn.ObjectId, OpenMode.ForWrite) as Circle ;
                Circle CircleIn2 = tr.GetObject(perCircleIn2.ObjectId, OpenMode.ForWrite) as Circle;

                D1 = Math.Round((CircleIn.Center - CircleIn2.Center).Y,3) ;

            //Для Выхода

            
                Circle CircleOut = tr.GetObject(perCircleOut.ObjectId, OpenMode.ForWrite) as Circle;
                Circle CircleOut2 = tr.GetObject(perCircleOut2.ObjectId, OpenMode.ForWrite) as Circle;

                D2 = Math.Round((CircleOut.Center - CircleOut2.Center).Y, 3);

               
                if (CircleIn.Center.Y> CircleOut.Center.Y)
                {
                    isRevers = true;
                };

                tr.Commit();
            }



            double alfa1 = pdrCircleInAngel.Value;
            double R1 = D1 / (1 - Math.Cos(pdrCircleInAngel.Value * Math.PI / 180));
            double l1 = 2 * Math.PI * R1 * alfa1 / 360;
            //Смещение точки
            double L1 = Math.Sqrt(Math.Pow(R1, 2) - Math.Pow(R1 - D1, 2));


            double alfa2 = pdrCircleOutAngel.Value;
            double R2 = D2 / (1 - Math.Cos(pdrCircleOutAngel.Value * Math.PI / 180));
            double l2 = 2 * Math.PI * R2 * alfa2 / 360;

            //Смещение точки
            double L2 = Math.Sqrt(Math.Pow(R2, 2) - Math.Pow(R2 - D2, 2));


            MyOpenDocument.ed.WriteMessage("\nα₁  = " + alfa1 + "°");
            MyOpenDocument.ed.WriteMessage("\nD₁ = " + Math.Round(D1, 3) + " м.");
            MyOpenDocument.ed.WriteMessage("\nR₁ = " + Math.Round(R1, 3) + " м.");
            MyOpenDocument.ed.WriteMessage("\nl₁(расчет.) = " + Math.Round(l1, 3) + " м.");
            MyOpenDocument.ed.WriteMessage("\nL₁(смещ.) = " + Math.Round(L1, 3) + " м.");

            MyOpenDocument.ed.WriteMessage("\n~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");

            MyOpenDocument.ed.WriteMessage("\nα₂ = " + alfa2 + "°");
            MyOpenDocument.ed.WriteMessage("\nD₂ = " + Math.Round(D2,3) + " м.");
            MyOpenDocument.ed.WriteMessage("\nR₂ = " + Math.Round(R2, 3) + " м.");
            MyOpenDocument.ed.WriteMessage("\nl₂(расчет.) = " + Math.Round(l2, 3) + " м.");
            MyOpenDocument.ed.WriteMessage("\nL₂(смещ.) = " + Math.Round(L2, 3) + " м.");


            if (isRevers) 
            {
                L1 = -L1;
                L2 = -L2;
            }

            MyOpenDocument.ed.WriteMessage("\nИнверсия:"+isRevers.ToString());



            Point3d coorR1;
            Point3d coorR2;
            using (var tr = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
            {

                // Вход
                Circle CircleIn = tr.GetObject(perCircleIn.ObjectId, OpenMode.ForWrite) as Circle;
                Circle CircleIn2 = tr.GetObject(perCircleIn2.ObjectId, OpenMode.ForWrite) as Circle;
                CircleIn2.Center = new Point3d(CircleIn.Center.X + L1, CircleIn2.Center.Y, CircleIn2.Center.Z);

                //координаты радиуса для построения
                coorR1 = CircleIn2.Center + new Point3d(0, R1, 0).GetAsVector();


                MyOpenDocument.ed.WriteMessage("\nКоординаты R₁ " + coorR1.X + "," + coorR1.Y);


                BlockTable bt = tr.GetObject(MyOpenDocument.dbCurrent.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;


                // 3. Вычисляем начальный и конечный углы в радианах
                Vector3d startVec = CircleIn.Center - coorR1;
                Vector3d endVec = CircleIn2.Center - coorR1;

                // Углы вычисляются относительно оси X в плоскости XY
                Plane plane = new Plane(Point3d.Origin, Vector3d.ZAxis);
                double startAngle = startVec.AngleOnPlane(plane);
                double endAngle = endVec.AngleOnPlane(plane);

                // 4. Создаем объект дуги (Arc)

                if (isRevers)
                {
                    // 1. Сохраняем startAngle во временную переменную
                    double temp = startAngle;
                    // 2. Присваиваем startAngle значение endAngle
                    startAngle = endAngle;
                    // 3. Присваиваем endAngle сохраненное значение из temp
                    endAngle = temp;
                }

                using
                (
                Arc arc = new Arc(coorR1, R1, startAngle, endAngle)
                )
                {
                    // 5. Добавляем дугу в чертеж
                    btr.AppendEntity(arc);
                    tr.AddNewlyCreatedDBObject(arc, true);
                }


                //Выход
                Circle CircleOut = tr.GetObject(perCircleOut.ObjectId, OpenMode.ForWrite) as Circle;
                Circle CircleOut2 = tr.GetObject(perCircleOut2.ObjectId, OpenMode.ForWrite) as Circle;
                CircleOut2.Center = new Point3d(CircleOut.Center.X - L2, CircleOut2.Center.Y, CircleOut2.Center.Z);


                //координаты радиуса для построения
                coorR2 = CircleOut2.Center + new Point3d(0, R2, 0).GetAsVector();


                MyOpenDocument.ed.WriteMessage("\nКоординаты R₂ " + coorR2.X + "," + coorR2.Y);

                Vector3d startVec2 = CircleOut.Center - coorR2;
                Vector3d endVec2 = CircleOut2.Center - coorR2;

                // Углы вычисляются относительно оси X в плоскости XY
                Plane plane2 = new Plane(Point3d.Origin, Vector3d.ZAxis);
                double startAngle2 = startVec2.AngleOnPlane(plane2);
                double endAngle2 = endVec2.AngleOnPlane(plane2);

                // 4. Создаем объект дуги (Arc)
                if (isRevers)
                {
                    // 1. Сохраняем startAngle во временную переменную
                    double temp = startAngle2;
                    // 2. Присваиваем startAngle значение endAngle
                    startAngle2 = endAngle2;
                    // 3. Присваиваем endAngle сохраненное значение из temp
                    endAngle2 = temp;
                }


                using 
                (
                    Arc arc = new Arc(coorR2, R2, endAngle2, startAngle2)
                )
                {
                    // 5. Добавляем дугу в чертеж
                    btr.AppendEntity(arc);
                    tr.AddNewlyCreatedDBObject(arc, true);
                }


                //Дорисовать полилинию
                if (CircleIn2.Center != CircleOut2.Center)
                {
                    Polyline poly = new Polyline();

                    poly.AddVertexAt(0,new Point2d(CircleIn2.Center.X, CircleIn2.Center.Y),0,0,0);
                    poly.AddVertexAt(1, new Point2d(CircleOut2.Center.X, CircleOut2.Center.Y), 0, 0, 0);
                    btr.AppendEntity(poly);
                    tr.AddNewlyCreatedDBObject(poly, true);

                }
                tr.Commit();
                MyOpenDocument.ed.WriteMessage("\nДуга успешно создана.");
            }
        }



        [CommandMethod("DrawCenteredPLAtIntersectionsWithMLeader")]
        public void DrawCenteredPLAtIntersectionsWithMLeader()
        {
            var doc = MyOpenDocument.doc;
            var db = MyOpenDocument.dbCurrent;
            var ed = MyOpenDocument.ed;

            //ed.Regen();

            // 1) Запрос длины новой полилинии
            var pdOpt = new PromptDoubleOptions("\nВведите полную длину новой полилинии:")
            {
                AllowNegative = false,
                AllowZero = false
            };
            var pdRes = ed.GetDouble(pdOpt);
            if (pdRes.Status != PromptStatus.OK) return;
            double fullLength = pdRes.Value;
            double halfLength = fullLength / 2.0;

            // 2) Нужно ли вставлять мультивыноски?
            bool needMLeader = false;
            bool immediateTextInput = false;
            bool autoTextFromLayer = false;
            string defaultText = "ХХХ";

            var pko = new PromptKeywordOptions("\nДобавлять мультивыноски на пересечениях? [Да/Нет]:", "Да Нет");
            var pkr = ed.GetKeywords(pko);
            if (pkr.Status == PromptStatus.OK && pkr.StringResult == "Да")
            {
                needMLeader = true;

                var pko2 = new PromptKeywordOptions("\nСразу ввести текст мультивыноски? [Да/Нет]:", "Да Нет");
                var pkr2 = ed.GetKeywords(pko2);
                if (pkr2.Status == PromptStatus.OK && pkr2.StringResult == "Да")
                {
                    immediateTextInput = true;
                }
            }

            // 3) Выбор основной полилинии
            PromptEntityOptions peo = new PromptEntityOptions("\nВыберите основную полилинию:");
            peo.SetRejectMessage("\nВыберите полилинию.");
            peo.AddAllowedClass(typeof(Polyline), true);

            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var mainPl = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;
                if (mainPl == null)
                {
                    ed.WriteMessage("\nНе удалось получить полилинию.");
                    return;
                }

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                var others = new List<Polyline>();

                // 1. Создаем фильтр, чтобы выбирать ТОЛЬКО полилинии

                //Филтр полилиний и проверка на замкнутость
                TypedValue[] filterValues = new TypedValue[]
                    {
                        // --- Начало логической группы "ИЛИ" ---
                        new TypedValue((int)DxfCode.Operator, "<OR"), 

                            
                        // Правило 2: Тип объекта - "LWPOLYLINE" (легковесная 2D полилиния)
                        new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),

                        // --- Конец логической группы "ИЛИ" ---
                        new TypedValue((int)DxfCode.Operator, "OR>")
                    };


                SelectionFilter filter = new SelectionFilter(filterValues);


                // 2. Просим редактор выбрать все объекты, соответствующие фильтру
                PromptSelectionResult psr = ed.SelectAll(filter);

                // 3. Работаем с уже отфильтрованным, гораздо меньшим набором
                if (psr.Status == PromptStatus.OK)
                {
                    foreach (ObjectId id in psr.Value.GetObjectIds())
                    {
                        if (id == mainPl.ObjectId) continue;

                        // Открываем полилинию напрямую, т.к. фильтр гарантирует тип
                        var pl = tr.GetObject(id, OpenMode.ForRead) as Polyline;

                        // Дополнительная проверка на случай, если что-то пошло не так (опционально, но рекомендуется)
                        if (pl == null) continue;

                        // Проводим проверки на видимость
                        if (lt.Has(pl.Layer))
                        {
                            var ltr = (LayerTableRecord)tr.GetObject(lt[pl.Layer], OpenMode.ForRead);
                            if (!ltr.IsOff && !ltr.IsFrozen && pl.Visible)
                            {
                                others.Add(pl);
                            }
                        }
                    }
                }



                // Перебор сегментов основной полилинии
                int segCount = mainPl.Closed ? mainPl.NumberOfVertices : mainPl.NumberOfVertices - 1;
                for (int i = 0; i < segCount; i++)
                {
                    var p1 = mainPl.GetPoint3dAt(i);
                    var p2 = mainPl.GetPoint3dAt((i + 1) % mainPl.NumberOfVertices);
                    double segmentLength = p1.DistanceTo(p2);
                    if (segmentLength < 1e-9) continue;

                    var segLine = new Line(p1, p2);
                    var dir = (p2 - p1).GetNormal();

                    // --- ШАГ 1: Находим все потенциальные отрезки на текущем сегменте ---
                    var potentialSegments = new List<LineSegment3d>();
                    foreach (var pl in others)
                    {
                        var ic = new Point3dCollection();
                        pl.IntersectWith(segLine, Intersect.OnBothOperands, ic, IntPtr.Zero, IntPtr.Zero);

                        foreach (Point3d ip in ic)
                        {
                            double effectiveLength = Math.Min(fullLength, segmentLength);
                            var start = ip - dir * (double)(effectiveLength / 2.0);
                            var end = ip + dir * (double)(effectiveLength / 2.0);
                            potentialSegments.Add(new LineSegment3d(start, end));
                        }
                    }

                    if (potentialSegments.Count == 0) continue;

                    // --- ШАГ 2: Сортируем и сливаем пересекающиеся отрезки ---
                    var mergedSegments = MergeOverlappingSegments(potentialSegments, p1);

                    // --- ШАГ 3: Создаем геометрию из слитых отрезков ---
                    string currentLayer = tr.GetObject(db.Clayer, OpenMode.ForRead) is LayerTableRecord currLtr ? currLtr.Name : "0";

                    foreach (var mergedSegment in mergedSegments)
                    {
                        // Создаем итоговую полилинию
                        var newPl = new Polyline();
                        newPl.AddVertexAt(0, new Point2d(mergedSegment.StartPoint.X, mergedSegment.StartPoint.Y), 0, 0, 0);
                        newPl.AddVertexAt(1, new Point2d(mergedSegment.EndPoint.X, mergedSegment.EndPoint.Y), 0, 0, 0);
                        newPl.Layer = currentLayer;
                        ObjectId newPlineId = ms.AppendEntity(newPl);
                        tr.AddNewlyCreatedDBObject(newPl, true);

                        // Добавляем мультивыноску в центр слитого сегмента
                        if (needMLeader)
                        {
                            Point3d leaderTarget = mergedSegment.MidPoint;
                            string textForLeader = defaultText;
                            if (immediateTextInput)
                            {
                                ZoomToEntity(newPlineId, 3.0);
                                var pStrOpts = new PromptStringOptions("\nВведите текст для мультивыноски:") { AllowSpaces = true };
                                var pStrRes = ed.GetString(pStrOpts);
                                textForLeader = (pStrRes.Status == PromptStatus.OK) ? pStrRes.StringResult : defaultText;
                            }

                            MLeader mLeader = CreateMLeader(leaderTarget, textForLeader, db);
                            if (mLeader != null)
                            {
                                mLeader.Layer = currentLayer;
                                ms.AppendEntity(mLeader);
                                tr.AddNewlyCreatedDBObject(mLeader, true);
                            }
                        }
                    }
                }


                tr.Commit();
            }

            ed.WriteMessage("\nОбработка завершена.");
        }

        private List<LineSegment3d> MergeOverlappingSegments(List<LineSegment3d> segments, Point3d sortOrigin)
        {
            if (segments.Count < 2) return segments;

            // Сортируем отрезки по расстоянию их стартовой точки от начала главного сегмента
            var sorted = segments.OrderBy(s => s.StartPoint.DistanceTo(sortOrigin)).ToList();

            var merged = new List<LineSegment3d>();
            var currentMerge = sorted[0];

            for (int i = 1; i < sorted.Count; i++)
            {
                var next = sorted[i];

                // Проверяем, что конец текущего сливаемого отрезка 'currentMerge'
                // находится дальше (или на том же уровне), чем начало следующего отрезка 'next'.
                // Это условие означает, что отрезки пересекаются или касаются.
                if (currentMerge.EndPoint.DistanceTo(sortOrigin) >= next.StartPoint.DistanceTo(sortOrigin) - 1e-9) // 1e-9 - допуск на погрешность
                {
                    // Есть пересечение. Расширяем текущий отрезок, выбирая самую дальнюю конечную точку.
                    var newEnd = currentMerge.EndPoint.DistanceTo(sortOrigin) > next.EndPoint.DistanceTo(sortOrigin)
                        ? currentMerge.EndPoint
                        : next.EndPoint;
                    currentMerge = new LineSegment3d(currentMerge.StartPoint, newEnd);
                }
                else
                {
                    // Пересечения нет. Завершаем текущий слитый отрезок, добавляем его в результат
                    // и начинаем новый сливаемый отрезок со следующего элемента.
                    merged.Add(currentMerge);
                    currentMerge = next;
                }
            }
            // Обязательно добавляем последний обработанный отрезок
            merged.Add(currentMerge);

            return merged;
        }






        // Вспомогательная функция создания мультивыноски
        private MLeader CreateMLeader(Point3d attachPoint, string text, Database db)
        {
            MLeader mleader = new MLeader();
            mleader.ContentType = ContentType.MTextContent;
            mleader.SetDatabaseDefaults();

            // Установка стиля мультивыноски по умолчанию из базы данных
            mleader.MLeaderStyle = db.MLeaderstyle;

            MText mtext = new MText
            {
                Contents = text,
                //TextHeight = 2.5 // Можно поменять на нужный размер
                Color = Color.FromColorIndex(ColorMethod.ByAci, 256) //Цвето по слою
            };

            int leaderIndex = mleader.AddLeader();
            int leaderLineIndex = mleader.AddLeaderLine(leaderIndex);
            mleader.AddFirstVertex(leaderLineIndex, attachPoint);
            mleader.AddLastVertex(leaderLineIndex, attachPoint + new Vector3d(1, 3, 0));
            mleader.MText = mtext;
            mleader.DoglegLength = 1; //длина полки
                                      //mleader.SetDogleg(leaderIndex, new Vector3d(0.7,0,0)); // отступ от стрелки
                                      // mleader.ArrowSize = 2.0; // размер стрелки
            mleader.LandingGap = 0.3; // расстояние между стрелкой и текстом

            return mleader;
        }




        [CommandMethod("йффф")]
        public void ExpandRectangle()
        {
            Document doc = MyOpenDocument.doc;
            Database db = MyOpenDocument.dbCurrent;
            Editor ed = MyOpenDocument.ed;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // 1. Выбор прямоугольной полилинии
                    PromptEntityOptions peo = new PromptEntityOptions("\nВыберите прямоугольную полилинию:");
                    peo.SetRejectMessage("\nВыберите только полилинию!");
                    peo.AddAllowedClass(typeof(Polyline), true);
                    PromptEntityResult per = ed.GetEntity(peo);
                    if (per.Status != PromptStatus.OK) return;

                    // 2. Проверка что это прямоугольник
                    Polyline pline = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;
                    if (!IsAxisAlignedRectangle(pline))
                    {
                        ed.WriteMessage("\nВыбрана не прямоугольная полилиния!");
                        return;
                    }

                    // 2. Выбор направления смещения
                    PromptKeywordOptions pko = new PromptKeywordOptions("\nВыберите направление смещения [Все/Горизонтально/Вертикально]:")
                    {
                        AllowArbitraryInput = false,
                    };
                    pko.Keywords.Add("Все", "ВСЕ", "Все стороны");
                    pko.Keywords.Add("Горизонтально", "ЛП", "Слева и справа");
                    pko.Keywords.Add("Вертикально", "ВН", "Сверху и снизу");
                    pko.Keywords.Default = "Все";

                    PromptResult pkr = ed.GetKeywords(pko);
                    if (pkr.Status != PromptStatus.OK) return;


                    // 3. Запрос значения расширения
                    PromptDoubleOptions pdo = new PromptDoubleOptions("\nВведите величину расширения:")
                    {
                        AllowNegative = true,
                    };
                    PromptDoubleResult pdr = ed.GetDouble(pdo);
                    if (pdr.Status != PromptStatus.OK) return;
                    double offset = pdr.Value;

                    // 4. Расчет нового прямоугольника
                    pline.UpgradeOpen();
                    ExtendRectangle(pline, offset, pkr.StringResult.ToUpper());

                    tr.Commit();
                    ed.Regen();
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\nОшибка: {ex.Message}");
                }
            }
        }

        private bool IsAxisAlignedRectangle(Polyline pline)
        {
            // Проверка на прямоугольник с 4 вершинами, выровненный по осям
            if (pline.NumberOfVertices != 4) return false;

            Point2dCollection points = new Point2dCollection();
            for (int i = 0; i < 4; i++)
            {
                points.Add(pline.GetPoint2dAt(i));
            }

            // Проверка прямых углов и параллельности сторон
            Vector2d v1 = points[1] - points[0];
            Vector2d v2 = points[2] - points[1];
            Vector2d v3 = points[3] - points[2];
            Vector2d v4 = points[0] - points[3];

            return v1.IsPerpendicularTo(v2) &&
                   v2.IsPerpendicularTo(v3) &&
                   v3.IsPerpendicularTo(v4);
        }

        private void ExtendRectangle(Polyline pline, double offset, string direction)
        {
            // Находим границы прямоугольника
            Extents2d extents = GetPolylineExtents(pline);
            double minX = extents.MinPoint.X;
            double maxX = extents.MaxPoint.X;
            double minY = extents.MinPoint.Y;
            double maxY = extents.MaxPoint.Y;

            switch (direction)
            {
                case "ВЕРТИКАЛЬНО": // Верх/Низ
                    minY -= offset;
                    maxY += offset;
                    break;

                case "ГОРИЗОНТАЛЬНО": // Лево/Право
                    minX -= offset;
                    maxX += offset;
                    break;

                default: // Все стороны
                    minX -= offset;
                    maxX += offset;
                    minY -= offset;
                    maxY += offset;
                    break;
            }



            // Обновляем вершины
            pline.SetPointAt(0, new Point2d(minX, minY));
            pline.SetPointAt(1, new Point2d(maxX, minY));
            pline.SetPointAt(2, new Point2d(maxX, maxY));
            pline.SetPointAt(3, new Point2d(minX, maxY));
        }


        public static Extents2d GetPolylineExtents(Polyline pline)
        {
            if (pline.NumberOfVertices == 0)
                throw new ArgumentException("Полилиния не содержит вершин");

            // Инициализация первыми координатами
            Point2d firstPoint = pline.GetPoint2dAt(0);
            double minX = firstPoint.X;
            double maxX = firstPoint.X;
            double minY = firstPoint.Y;
            double maxY = firstPoint.Y;

            // Обход всех вершин
            for (int i = 1; i < pline.NumberOfVertices; i++)
            {
                Point2d pt = pline.GetPoint2dAt(i);

                minX = Math.Min(minX, pt.X);
                maxX = Math.Max(maxX, pt.X);
                minY = Math.Min(minY, pt.Y);
                maxY = Math.Max(maxY, pt.Y);
            }

            return new Extents2d(
                new Point2d(minX, minY),
                new Point2d(maxX, maxY)
            );
        }







        private ItemElement getMext(IsCheck Is)
        {
            ItemElement resultItem = new ItemElement();
            List<Handle> tempListHandle = new List<Handle>();
            List<ObjectId> tempObjectID = new List<ObjectId>();


            PromptSelectionResult acSSPrompt = MyOpenDocument.ed.GetSelection();

            if (acSSPrompt.Status == PromptStatus.OK)
            {
                SelectionSet acSSet = acSSPrompt.Value;
                using (Transaction trAdding = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject acSSObj in acSSet)
                    {
                        if (acSSObj != null)
                        {

                            ObjectId objId = acSSObj.ObjectId;
                            Entity entity = trAdding.GetObject(objId, OpenMode.ForRead) as Entity;

                            if (entity != null)
                            {
                                if (entity is MText)
                                {
                                    MText getObject2 = (MText)(object)entity;
                                    tempListHandle.Add(entity.ObjectId.Handle);
                                    tempObjectID.Add(entity.ObjectId);

                                    double doleValue = 0;

#if nanoCAD
                                    bool isAdd = double.TryParse(getObject2.Text.Trim().Replace(".", ","), out doleValue);
#else
                                    bool isAdd = double.TryParse(getObject2.Contents.Replace(",", "."), out doleValue);
#endif

                                    if (isAdd)
                                    {
                                        resultItem.result = resultItem.result + doleValue;
                                    }
                                    else
                                    {
                                        trAdding.Commit();
                                        ZoomToEntity(objId, 10);
                                        MyOpenDocument.ed.WriteMessage("\n\n Ты где-то ошибся, есть нечисловой текст \n Перепроверь, я тут подожду.");
                                        return null;
                                    }
                                }
                            }

                        }
                    }
                    trAdding.Commit();
                }
                resultItem.AllHandel = new List<Handle>(tempListHandle);
                resultItem.AllObjectID = new List<ObjectId>(tempObjectID);
            }

            if (Is == IsCheck.count)
            {
                resultItem.result = resultItem.AllHandel.Count();
            }

            if (Is == IsCheck.ave)
            {
                resultItem.result = resultItem.result / resultItem.AllHandel.Count();
            }
            return resultItem;
        }

        private ItemElement getOther(IsCheck Is)
        {
            ItemElement resultItem = new ItemElement();
            List<Handle> tempListHandle = new List<Handle>();
            List<ObjectId> tempObjectID = new List<ObjectId>();


            PromptSelectionResult acSSPrompt = MyOpenDocument.ed.GetSelection();

            if (acSSPrompt.Status == PromptStatus.OK)
            {
                SelectionSet acSSet = acSSPrompt.Value;
                using (Transaction trAdding = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
                {

                    foreach (SelectedObject acSSObj in acSSet)
                    {
                        if (acSSObj != null)
                        {

                            ObjectId objId = acSSObj.ObjectId;
                            Entity entity = trAdding.GetObject(objId, OpenMode.ForRead) as Entity;

                            if (entity != null)
                            {
                                if (Is == IsCheck.mleader)
                                {
                                    if (entity is MLeader)
                                    {
                                        tempListHandle.Add(entity.ObjectId.Handle);
                                        tempObjectID.Add(entity.ObjectId);
                                    }
                                }
                                else
                                {
                                    tempListHandle.Add(entity.ObjectId.Handle);
                                    tempObjectID.Add(entity.ObjectId);
                                }
                            }

                        }
                    }
                    trAdding.Commit();
                }
                resultItem.AllHandel = new List<Handle>(tempListHandle);
                resultItem.AllObjectID = new List<ObjectId>(tempObjectID);
            }

            resultItem.result = resultItem.AllHandel.Count();

            return resultItem;
        }



        private ItemElement getDimension(IsCheck Is)
        {
            ItemElement resultItem = new ItemElement();
            List<Handle> tempListHandle = new List<Handle>();
            List<ObjectId> tempObjectID = new List<ObjectId>();


            PromptSelectionResult acSSPrompt = MyOpenDocument.ed.GetSelection();

            if (acSSPrompt.Status == PromptStatus.OK)
            {
                SelectionSet acSSet = acSSPrompt.Value;
                using (Transaction trAdding = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
                {

                    foreach (SelectedObject acSSObj in acSSet)
                    {
                        if (acSSObj != null)
                        {



                            ObjectId objId = acSSObj.ObjectId;

                            //Проверка на dim
                            if (trAdding.GetObject(objId, OpenMode.ForRead) is Dimension)
                            {

                                Dimension entity = trAdding.GetObject(objId, OpenMode.ForRead) as Dimension;

                                tempListHandle.Add(entity.ObjectId.Handle);
                                tempObjectID.Add(entity.ObjectId);


                                bool isHandresultMeasurement = string.IsNullOrEmpty(entity.DimensionText); //если вручную текст вбит 

                                if (!isHandresultMeasurement)
                                {
                                    double doleValue = 0;
                                    bool isAdd = double.TryParse(entity.DimensionText.Trim().Replace(".", ","), out doleValue);

                                    if (isAdd) //Проверка может ли в число преобразовать. 
                                    {
                                        //Фиктивные
                                        resultItem.ObjSelID.Add(objId);
                                        resultItem.result = resultItem.result + doleValue;
                                    }
                                    else
                                    {
                                        trAdding.Commit();
                                        ZoomToEntity(objId, 10);
                                        MyOpenDocument.ed.WriteMessage("\n\n Ты где-то ошибся, есть нечисловой текст \n Перепроверь, я тут подожду.");
                                        return null;
                                    }
                                }
                                else
                                {
                                    double resultMeasurement = Math.Round(entity.Measurement, entity.Dimdec);  //Измеренное значение и учетом округления
                                    resultItem.result = resultItem.result + resultMeasurement;
                                }


                            }
                        }
                    }
                    trAdding.Commit();
                }
            }

            resultItem.AllHandel = tempListHandle;
            resultItem.AllObjectID = new List<ObjectId>(tempObjectID);


            if (Is == IsCheck.count)
            {
                resultItem.result = resultItem.AllHandel.Count();
            }

            if (Is == IsCheck.ave)
            {
                resultItem.result = resultItem.result / resultItem.AllHandel.Count();
            }

            return resultItem;

        }



        [CommandMethod("цфффф", CommandFlags.Modal)]
        public void RoundPolylineSegmentsExtended()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            // 1. Выбор полилинии
            var peo = new PromptEntityOptions("\nВыберите полилинию: ");
            peo.SetRejectMessage("\nНужно выбрать полилинию.");
            peo.AddAllowedClass(typeof(Polyline), true);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            // 2. Выбор метода округления
            var pko = new PromptKeywordOptions("\nМетод округления сегментов: ");
            pko.Keywords.Add("Точность");    // по знакам
            pko.Keywords.Add("Кратность");   // по шагу
            pko.Keywords.Default = "Точность";
            var pkr = ed.GetKeywords(pko);
            if (pkr.Status != PromptStatus.OK) return;

            bool usePrecision = pkr.StringResult == "Точность";

            int decimals = 0;
            double step = 0.0;

            if (usePrecision)
            {
                // 2a. Запрос точности (знаков после запятой)
                var pio = new PromptIntegerOptions("\nУкажите число знаков после запятой (0–8): ");
                pio.AllowNegative = false;
                pio.DefaultValue = 0;
                pio.LowerLimit = 0;
                pio.UpperLimit = 8;
                var pir = ed.GetInteger(pio);
                if (pir.Status != PromptStatus.OK) return;
                decimals = pir.Value;
            }
            else
            {
                // 2b. Запрос шага (кратности)
                var pdo = new PromptDoubleOptions("\nВведите шаг (кратность) сегмента, > 0: ");
                pdo.AllowNegative = false;
                pdo.AllowZero = false;
                pdo.DefaultValue = 1.0;
                var pdr = ed.GetDouble(pdo);
                if (pdr.Status != PromptStatus.OK) return;
                step = pdr.Value;
            }

            // 3. Обработка полилинии
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var pline = tr.GetObject(per.ObjectId, OpenMode.ForWrite) as Polyline;
                if (pline == null)
                {
                    ed.WriteMessage("\nНе удалось открыть полилинию.");
                    return;
                }

                // если была замкнута, размыкаем на время
                bool wasClosed = pline.Closed;
                if (wasClosed) pline.Closed = false;

                for (int i = 0; i < pline.NumberOfVertices - 1; i++)
                {
                    var p1 = pline.GetPoint3dAt(i);
                    var p2 = pline.GetPoint3dAt(i + 1);
                    double length = p1.DistanceTo(p2);
                    double newLen;

                    if (usePrecision)
                    {
                        // округляем до заданного числа знаков
                        newLen = Math.Round(length, decimals);
                    }
                    else
                    {
                        // берём наибольшую кратную step длину ≤ исходной
                        double cnt = Math.Floor(length / step);
                        newLen = cnt * step;
                        if (newLen <= 0) newLen = length;
                    }

                    // направление
                    var dir = (p2 - p1).GetNormal();
                    var p2new = p1 + dir * newLen;

                    // обновление вершины (только XY)
                    pline.SetPointAt(i + 1, new Point2d(p2new.X, p2new.Y));
                }

                // возвращаем замкнутость
                if (wasClosed) pline.Closed = true;

                tr.Commit();
            }

            // 4. Вывод результата
            if (usePrecision)
                ed.WriteMessage($"\nСегменты округлены до {decimals} знаков после запятой.");
            else
                ed.WriteMessage($"\nСегменты округлены по шагу {step}.");
        }







        string creatPromptKeywordOptions(string textName, List<string> listOptions, int defaultOptions)
        {
            PromptKeywordOptions options = new PromptKeywordOptions(textName);

            foreach (string itemString in listOptions)
            {
                options.Keywords.Add(itemString);
            }
            options.Keywords.Default = listOptions[defaultOptions - 1]; // если сам, то -1

            PromptResult result = MyOpenDocument.ed.GetKeywords(options);
            if (result.Status == PromptStatus.OK)
            {
                MyOpenDocument.ed.WriteMessage("Вы выбрали : " + result.StringResult);

            }
            else
            {
                MyOpenDocument.ed.WriteMessage("\n\nОтмена.\n");
                return null;
            }




            return result.StringResult;
        }


        public void UpdateTextById(ObjectId textId, string newText, int colorIndex)
        {

            using (Transaction tr = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
            {
                try
                {
                    MText mtextEntity = tr.GetObject(textId, OpenMode.ForWrite) as MText;

                    if (mtextEntity != null)
                    {
                        // Изменяем текст
                        mtextEntity.Contents = newText;

                        // Пример других изменений свойств (необязательно)
                        mtextEntity.ColorIndex = colorIndex; // Например, изменяем цвет текста

                        // Завершаем транзакцию
                        tr.Commit();
                    }
                    else
                    {
                        // Обработка случая, если не удалось получить объект MText
                        MyOpenDocument.ed.WriteMessage("Unable to open MText with ObjectId: {0}\n", textId);
                    }
                }

                catch (System.Exception ex)
                {
                    // Обработка ошибок
                    // ed.WriteMessage("Error updating MText: {0}\n", ex.Message);
                    tr.Abort();
                }
            }

        }

        public void SelectObjects(List<ObjectId> objectIds)
        {
            try
            {
                MyOpenDocument.ed.SetImpliedSelection(objectIds.ToArray());
            }

            catch (Exception ex)
            {
                MyOpenDocument.ed.WriteMessage("Я думаю, скорее всего надо сделать восстановление по Handel.");
                return;
            }
        }





        public void ZoomToEntity(ObjectId entityId, double zoomPercent)
        {



            using (DocumentLock doclock = MyOpenDocument.doc.LockDocument())
            {

                using (Transaction tr = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
                {
                    Entity entity = tr.GetObject(entityId, OpenMode.ForRead) as Entity;

                    if (entity != null)
                    {
                        Extents3d extents = entity.GeometricExtents;

                        // Определение точек пределов объекта
                        Point3d minPoint = extents.MinPoint;
                        Point3d maxPoint = extents.MaxPoint;

                        // Создание новой записи представления
                        using (ViewTableRecord view = new ViewTableRecord())
                        {
                            // Задание пределов представления
                            view.CenterPoint = new Point2d((minPoint.X + maxPoint.X) / 2, (minPoint.Y + maxPoint.Y) / 2);
                            view.Height = (maxPoint.Y - minPoint.Y) * zoomPercent;
                            view.Width = (maxPoint.X - minPoint.X) * zoomPercent;

                            // Установка представления текущим
                            MyOpenDocument.ed.SetImpliedSelection(new ObjectId[] { entityId });
                            MyOpenDocument.ed.SetCurrentView(view);
                            MyOpenDocument.ed.CurrentUserCoordinateSystem = Matrix3d.Identity;
                        }
                    }

                    tr.Commit();
                }
            }
        }












        public static Point3dCollection createCirclePolygon(Point3d center, double radius, int segments)
        {
            Point3dCollection points = new Point3dCollection();
            for (int i = 0; i < segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                double x = center.X + radius * Math.Cos(angle);
                double y = center.Y + radius * Math.Sin(angle);
                points.Add(new Point3d(x, y, 0));
            }
            points.Add(points[0]); // Замыкаем многоугольник, добавляя первую точку в конец
            return points;
        }

        //Макс страниц
        private const int MaxLayouts = 50;
        private bool _isTest = false;
        private int _firstSheetNumber = 1;

        [CommandMethod("ADDLAY")]
        public void AddLayouts()
        {
            Document doc = MyOpenDocument.doc;
            Database db = MyOpenDocument.dbCurrent;
            Editor ed = MyOpenDocument.ed;

            int originalSelectionCycling = (int)Application.GetSystemVariable("SELECTIONCYCLING");
            Application.SetSystemVariable("SELECTIONCYCLING", 0);
            Application.SetSystemVariable("CTAB", "Model");

            string layerName = "";

            using (Transaction tr = ed.Document.TransactionManager.StartTransaction())
            {
                // Выбор объекта для определения слоя
                PromptEntityOptions item = new PromptEntityOptions("\n Выбирите поллилинию принадлежащию одному слою\n");
                PromptEntityResult perItem = MyOpenDocument.ed.GetEntity(item);

                Entity ent = tr.GetObject(perItem.ObjectId, OpenMode.ForRead) as Entity;
                if (ent != null)
                {
                    layerName = ent.Layer;
                }
                tr.Commit();
            }

            // Получение объектов на слое
            List<ObjectId> formatIds = GetFormatIdsOnLayer(db, layerName);
            if (formatIds.Count == 0 || formatIds.Count > MaxLayouts)
            {
                ed.WriteMessage($"\nСлой \"{layerName}\" содержит {formatIds.Count} рамок. Должно быть от 1 до {MaxLayouts}.");
                return;
            }

            // Запрос масштаба
            double scale = GetScale(ed) ?? 0.5;

            // Получение ограничивающих рамок
            List<Tuple<ObjectId, Point3d, Point3d>> boundingBoxes = GetBoundingBoxes(formatIds, db);

            // Сортировка рамок
            boundingBoxes = SortBoundingBoxes(boundingBoxes);


            //CreateLayoutsAndViewportsFromPolylines(db, ed, formatIds, scale);
            CreateLayoutsAndViewports(db, ed, boundingBoxes, scale);


            Application.SetSystemVariable("CTAB", "Model");

        }
        private List<ObjectId> GetFormatIdsOnLayer(Database db, string layerName)
        {
            List<ObjectId> ids = new List<ObjectId>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;

                foreach (ObjectId id in modelSpace)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent != null && ent.Layer == layerName)
                    {
                        ids.Add(id);
                    }
                }

                tr.Commit();
            }

            return ids;
        }





        private double? GetScale(Editor ed)
        {
            PromptDoubleOptions pdo = new PromptDoubleOptions("\nМасштаб 1:<0.5>")
            {
                AllowNegative = false,
                AllowZero = false,
                DefaultValue = 0.5,
                UseDefaultValue = true
            };

            PromptDoubleResult pdr = ed.GetDouble(pdo);
            return pdr.Status == PromptStatus.OK ? pdr.Value : (double?)null;
        }


        /// <summary>
        /// Возвращает для каждого переданного объекта его ObjectId и ограничивающий прямоугольник (MinPoint, MaxPoint).
        /// </summary>
        private List<Tuple<ObjectId, Point3d, Point3d>> GetBoundingBoxes(List<ObjectId> entityIds, Database db)
        {
            var result = new List<Tuple<ObjectId, Point3d, Point3d>>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in entityIds)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null)
                        continue;

                    Extents3d ext;
                    if (ent is BlockReference br && br.IsDynamicBlock)
                    {
                        // Специально для динамических блоков
                        ext = GetDynamicBlockBounds(br);
                    }
                    else
                    {
                        try
                        {
                            ext = ent.GeometricExtents;
                        }
                        catch
                        {
                            // У объекта нет экстентов — пропускаем
                            continue;
                        }
                    }

                    // Добавляем в результат кортеж: (ObjectId, MinPoint, MaxPoint)
                    result.Add(Tuple.Create(id, ext.MinPoint, ext.MaxPoint));
                }

                tr.Commit();
            }

            return result;
        }


        private Extents3d GetDynamicBlockBounds(BlockReference br)
        {
            // Упрощенная реализация для динамических блоков
            // В реальном коде потребуется более сложная обработка
            return br.GeometricExtents;
        }

        /// <summary>
        /// Сортирует список (ObjectId, MinPoint, MaxPoint) по X или Y координате в зависимости от формы.
        /// </summary>
        private List<Tuple<ObjectId, Point3d, Point3d>> SortBoundingBoxes(
            List<Tuple<ObjectId, Point3d, Point3d>> boxes)
        {
            // Вычисляем максимальную ширину и высоту для принятия решения о направлении сортировки
            double maxWidth = boxes.Max(t => t.Item3.X - t.Item2.X);
            double maxHeight = boxes.Max(t => t.Item3.Y - t.Item2.Y);

            if (maxWidth > maxHeight)
            {
                // Сортируем слева направо по минимальной X-координате
                return boxes
                    .OrderBy(t => t.Item2.X)
                    .ToList();
            }
            else
            {
                // Сортируем сверху вниз по минимальной Y-координате
                return boxes
                    .OrderByDescending(t => t.Item2.Y)
                    .ToList();
            }
        }












        private void CreateLayoutsAndViewports(
          Database db,
          Editor ed,
          List<Tuple<ObjectId, Point3d, Point3d>> boxesWithIds,
          double scale)
        {
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // Сортируем входящий список
                boxesWithIds = SortBoundingBoxes(boxesWithIds);

                LayoutManager lm = LayoutManager.Current;
                DBDictionary layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForWrite);

                for (int i = 0; i < boxesWithIds.Count; i++)
                {
                    // Получаем кортеж из списка
                    var tuple = boxesWithIds[i];
                    ObjectId polyId = tuple.Item1;
                    Point3d minPt = tuple.Item2;
                    Point3d maxPt = tuple.Item3;

                    // 1) Создаём или получаем Layout
                    string layoutName = (_firstSheetNumber + i).ToString();
                    ObjectId layoutId;
                    Layout layout;

                    if (layoutDict.Contains(layoutName))
                    {
                        layoutId = layoutDict.GetAt(layoutName);
                        layout = (Layout)tr.GetObject(layoutId, OpenMode.ForWrite);
                    }
                    else
                    {
                        layoutId = lm.CreateLayout(layoutName);
                        layout = (Layout)tr.GetObject(layoutId, OpenMode.ForWrite);
                    }

                    // Активируем его
                    lm.CurrentLayout = layoutName;

                    // Пространство листа
                    var paperSpace = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite);

                    // 2) Вычисляем размеры и центр области модели
                    Point3d centerModel = new Point3d(
                        (minPt.X + maxPt.X) / 2.0,
                        (minPt.Y + maxPt.Y) / 2.0,
                        0);
                    double width = (maxPt.X - minPt.X) / scale;
                    double height = (maxPt.Y - minPt.Y) / scale;

                    // 3) Позиция Viewport в центре листа
                    double paperW = layout.PlotPaperSize.X;
                    double paperH = layout.PlotPaperSize.Y;
                    Point3d centerSheet = new Point3d(paperW / 2.0, paperH / 2.0, 0);

                    // 4) Создаём Viewport
                    var vp = new Viewport();
                    paperSpace.AppendEntity(vp);
                    tr.AddNewlyCreatedDBObject(vp, true);

                    // Устанавливаем его геометрию на листе
                    vp.UpgradeOpen();
                    vp.CenterPoint = centerSheet;
                    vp.Width = width;
                    vp.Height = height;


                    // 5) Настраиваем, что показывать из модели
                    vp.ViewCenter = new Point2d(centerModel.X, centerModel.Y);
                    vp.ViewTarget = new Point3d(centerModel.X, centerModel.Y, 0);
                    vp.ViewDirection = Vector3d.ZAxis;
                    vp.ViewHeight = height;
                    vp.TwistAngle = 0.0;          // без поворота
                    vp.CustomScale = 1.0 / scale;
                    vp.Locked = true;
                    vp.On = true;

                    // 6) Присоединяем полилинию-контур к листу и включаем клиппинг
                    Polyline pl = (Polyline)tr.GetObject(polyId, OpenMode.ForRead);
                    Polyline clone = (Polyline)pl.Clone();
                    paperSpace.AppendEntity(clone);
                    tr.AddNewlyCreatedDBObject(clone, true);

                    vp.NonRectClipEntityId = clone.ObjectId;
                    vp.NonRectClipOn = true;
                    vp.DowngradeOpen();
                }

                tr.Commit();
            }
        }






        public EntMtextOrDimToSumOrCount()
        {

            MyOpenDocument.ed = Application.DocumentManager.MdiActiveDocument.Editor;
            MyOpenDocument.doc = Application.DocumentManager.MdiActiveDocument;
            MyOpenDocument.dbCurrent = Application.DocumentManager.MdiActiveDocument.Database;

            this._tools = new Serialize(MyOpenDocument.doc, MyOpenDocument.dbCurrent, MyOpenDocument.ed);
            MyOpenDocument.ed.WriteMessage("Loading... EntMtextOrDimToSumOrCount | AeroHost 2025г. | ver. 1.7");
            MyOpenDocument.ed.WriteMessage("\n");
            MyOpenDocument.ed.WriteMessage("| йф - Функция подсчета суммы\\количества (MTexta,размеров или прочее).");
            MyOpenDocument.ed.WriteMessage("| йфф - Восстановление набора выделенных объектов (по Handle).");
            MyOpenDocument.ed.WriteMessage("| йффф - Расширяем\\сужаем существующую полиллинию на заданное расстояние.");
            MyOpenDocument.ed.WriteMessage("| цф - Разворачивает профиль линии в прямую с высотами.");
            MyOpenDocument.ed.WriteMessage("| цфф - Построить точку высотной отметки интерполяцией между двумя другими точки.");
            MyOpenDocument.ed.WriteMessage("| цффф - Построить размеры над полиллинией, отделеные другими полиллиниями или вложенным.");
            MyOpenDocument.ed.WriteMessage("| цфффф - Округлить по количеству знаков или кратости сегменты полиллинии.");
            MyOpenDocument.ed.WriteMessage("| DrawCenteredPLAtIntersectionsWithMLeader - Расставляет полиллинию вдоль определенного размера с возможность отображения мультивыноски.");
            //MyOpenDocument.ed.WriteMessage("| йц - Скрытие фона у mTexta и Выносок");
            MyOpenDocument.ed.WriteMessage("\n");

        }

    }
}





