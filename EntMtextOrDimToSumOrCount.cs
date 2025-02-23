
#region Namespaces


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using EntMtextOrDimToSumOrCount;










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

            //Обновляем текст в Мтекст
            UpdateTextById(perItem.ObjectId, temp.result.ToString(), 256);

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

                    ObjectId[] allentity;
                    using (Transaction tr = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
                    {
                        // Используем транзакцию для открытия таблицы объектов
                        BlockTable bt = tr.GetObject(MyOpenDocument.dbCurrent.BlockTableId, OpenMode.ForRead) as BlockTable;
                        BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                        allentity = modelSpace.Cast<ObjectId>().ToArray();
                        tr.Commit();
                    }

                    using (Transaction tr = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
                    {
                        stopwatch.Start();
                        await Task.Run
                        (
                                () =>
                             {

                                 foreach (string itemHandelString in selectionItem.SerializedAllHandel)
                                 {
                                     foreach (ObjectId objectId in allentity)
                                     {
                                         Entity entity = tr.GetObject(objectId, OpenMode.ForRead) as Entity;

                                         if (entity != null && entity.Handle.ToString() == itemHandelString)
                                         {
                                             // Добавляем ObjectID, если handle равны
                                             tempList.Add(objectId);
                                             // Может потребоваться прервать внутренний цикл, если соответствие найдено
                                             break;
                                         }
                                     }
                                 }


                                 /*
                                 //Переделал в паралельные потоки 
                                 Parallel.ForEach
                                 (selectionItem.SerializedAllHandel, itemHandelString =>
                                 //foreach (string itemHandelString in selectionItem.SerializedAllHandel)
                                     {
                                         foreach (ObjectId objectId in allentity)
                                         {
                                             Entity entity = tr.GetObject(objectId, OpenMode.ForWrite) as Entity;
                                             if (entity == null)
                                             {
                                                 entity = tr.TransactionManager.GetObject(objectId, OpenMode.ForWrite) as Entity;
                                             }

                                             // Проверяем, совпадает ли handle объекта с искомым
                                             if (entity != null && entity.Handle.ToString() == itemHandelString)
                                             {
                                                 //Добавляем Object ID Если handek равын
                                                 tempList.Add(objectId);
                                                 break;
                                             }
                                         }
                                     }
                                 );*/
                             }
                         );


                        stopwatch.Stop();
                        MyOpenDocument.ed.WriteMessage("Прошло с момента операции:  " + stopwatch.Elapsed.TotalSeconds + " c.");
                        tr.Commit();
                        SelectObjects(tempList);
                    }

                }
            }
            catch (Exception ex)
            {
                MyOpenDocument.ed.WriteMessage(ex.ToString());
                return;
            }

        }

        [CommandMethod("йффф", CommandFlags.UsePickSet |
                      CommandFlags.Redraw | CommandFlags.Modal)] // название команды, вызываемой в Autocad
        public void inDataSummObjId()

        {
            if (MyOpenDocument.doc == null) return;

            PromptEntityOptions item = new PromptEntityOptions("\n ObjectID Выберите объект(Mtext) что б вернуть выделение: \n");
            PromptEntityResult perItem = MyOpenDocument.ed.GetEntity(item);
            ItemElement selectionItem = _tools.ShowExtensionDictionaryContents<ItemElement>(perItem.ObjectId, "Makarov.D_entMtextOrDimensionToSum");
            if (perItem.Status != PromptStatus.OK)
            {
                MyOpenDocument.ed.WriteMessage("Отмена");
                return;
            }
            try
            {
                if (selectionItem != null)
                {
                    List<ObjectId> tempList = new List<ObjectId>();

                    if (selectionItem.SerializedAllObjectID.Any())

                    {
                        //tempList = selectionItem.SerializedAllObjectID.Select(objId => new ObjectId(new IntPtr(objId))).Where(objId => objId.IsValid & !objId.IsErased & !objId.IsNull).ToList();
                        tempList = selectionItem.SerializedAllObjectID.Select(objId => new ObjectId(new IntPtr(objId))).ToList();
                        SelectObjects(tempList);
                    }
                    else
                    {
                        return;
                    }

                }

            }
            catch (Exception ex)
            {
                MyOpenDocument.ed.WriteMessage("Не могу найти по Object ID(использовать только в ТЕКУЩЕЙ СЕССИИ), возможно надо по Handel.");

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

        //По 4 точкам в пространстве 
        [CommandMethod("цффф", CommandFlags.UsePickSet |
                     CommandFlags.Redraw | CommandFlags.Modal)] // название команды, вызываемой в Autocadw
        public void FindZValue()
        {

            MyOpenDocument.ed.WriteMessage("Билинейная интерполяция.Она учитывает изменения по обеим осям. Надо выбрать 4 точки в пространстве");
            // Начало транзакции
            using (Transaction tr = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
            {

                // Запрашиваем у пользователя ввод точки
                PromptPointOptions ppo1 = new PromptPointOptions("\nУкажите местоположение точки (Верхний левый):");
                PromptPointResult ppr1 = MyOpenDocument.ed.GetPoint(ppo1);

                if (ppr1.Status != PromptStatus.OK)
                {
                    MyOpenDocument.ed.WriteMessage("\nТочка не была выбрана.");
                    return;
                }

                // Запрашиваем у пользователя ввод точки
                PromptPointOptions ppo2 = new PromptPointOptions("\nУкажите местоположение точки (Верхний правый):");
                PromptPointResult ppr2 = MyOpenDocument.ed.GetPoint(ppo2);

                if (ppr2.Status != PromptStatus.OK)
                {
                    MyOpenDocument.ed.WriteMessage("\nТочка не была выбрана.");
                    return;
                }

                // Запрашиваем у пользователя ввод точки
                PromptPointOptions ppo3 = new PromptPointOptions("\nУкажите местоположение точки (Нижний левый):");
                PromptPointResult ppr3 = MyOpenDocument.ed.GetPoint(ppo3);

                if (ppr3.Status != PromptStatus.OK)
                {
                    MyOpenDocument.ed.WriteMessage("\nТочка не была выбрана.");
                    return;
                }

                // Запрашиваем у пользователя ввод точки
                PromptPointOptions ppo4 = new PromptPointOptions("\nУкажите местоположение точки (Нижний правый):");
                PromptPointResult ppr4 = MyOpenDocument.ed.GetPoint(ppo4);

                if (ppr4.Status != PromptStatus.OK)
                {
                    MyOpenDocument.ed.WriteMessage("\nТочка не была выбрана.");
                    return;
                }


                // Запрашиваем у пользователя ввод точки
                PromptPointOptions ppo = new PromptPointOptions("\nУкажите местоположение точки:");
                PromptPointResult ppr = MyOpenDocument.ed.GetPoint(ppo);

                if (ppr.Status != PromptStatus.OK)
                {
                    MyOpenDocument.ed.WriteMessage("\nТочка не была выбрана.");
                    return;
                }

                // Получаем координаты точки, которую указал пользователь
                Point3d targetPoint = ppr.Value;
                double Xtarget = targetPoint.X;
                double Ytarget = targetPoint.Y;
                MyOpenDocument.ed.WriteMessage("\n" + targetPoint.X + " " + targetPoint.Y + " " + targetPoint.Z);

                // Определяем четыре известные точки (с известными Z)
                Point3d p1 = ppr1.Value; // Верхний левый
                Point3d p2 = ppr2.Value; // Верхний правый
                Point3d p3 = ppr3.Value; // Нижний левый
                Point3d p4 = ppr4.Value; // Нижний правый

                // Проверяем, что точка находится в пределах прямоугольника
                if (Xtarget >= p1.X && Xtarget <= p2.X && Ytarget >= p1.Y && Ytarget <= p3.Y)
                {
                    // Выполняем билинейную интерполяцию для нахождения значения Z
                    double Ztarget = BilinearInterpolateZ(p1, p2, p3, p4, Xtarget, Ytarget);

                    // Выводим значение Z в командное окно
                    MyOpenDocument.ed.WriteMessage($"\nЗначение Z в указанной точке ({Xtarget}, {Ytarget}): {Ztarget}");
                }
                else
                {
                    MyOpenDocument.ed.WriteMessage("\nТочка выходит за пределы области интерполяции.");
                }

                // Завершаем транзакцию
                tr.Commit();
            }
        }

        // Метод для билинейной интерполяции
        public double BilinearInterpolateZ(Point3d p1, Point3d p2, Point3d p3, Point3d p4, double Xtarget, double Ytarget)
        {
            double X1 = p1.X, Y1 = p1.Y, Z1 = p1.Z; // Точка 1 (верхний левый)
            double X2 = p2.X, Y2 = p2.Y, Z2 = p2.Z; // Точка 2 (верхний правый)
            double X3 = p3.X, Y3 = p3.Y, Z3 = p3.Z; // Точка 3 (нижний левый)
            double X4 = p4.X, Y4 = p4.Y, Z4 = p4.Z; // Точка 4 (нижний правый)

            // Билинейная интерполяция
            double Ztarget =
                Z1 * ((X2 - Xtarget) * (Y3 - Ytarget)) / ((X2 - X1) * (Y3 - Y1)) +
                Z2 * ((Xtarget - X1) * (Y3 - Ytarget)) / ((X2 - X1) * (Y3 - Y1)) +
                Z3 * ((X2 - Xtarget) * (Ytarget - Y1)) / ((X2 - X1) * (Y3 - Y1)) +
                Z4 * ((Xtarget - X1) * (Ytarget - Y1)) / ((X2 - X1) * (Y3 - Y1));

            return Ztarget;
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

            Text.creatText("Высоты_Makarov.D", new Point2d(targetPoint.X, targetPoint.Y), (Math.Round (Ztarget,2)).ToString(), "1", 256,0);


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
            double dTotal = new Point3d(X1, Y1, 0).DistanceTo(new Point3d(X2,Y2,0));

           // double dTotal = point1.DistanceTo(point2);

            //ТУТ НАДО БЕЗ Z Иначе типо неправильно считает, хотя правильно
            // Вычисляем расстояние от первой точки до целевой точки
            double dTarget = new Point3d(X1, Y1, 0).DistanceTo(new Point3d(targetPoint.X, targetPoint.Y, 0));

            // Линейная интерполяция
            double Ztarget = Z1 + (dTarget / dTotal) * (Z2 - Z1);
           // MyOpenDocument.ed.WriteMessage("Z1 " +Z1+ " dTarget "+ dTarget+ " dTotal " + dTotal + " Z2 "+Z2+ " Z1 "+ Z1 + "      "+Ztarget);
                        
            return Ztarget;
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

                foreach (SelectedObject acSSObj in acSSet)
                {
                    if (acSSObj != null)
                    {
                        using (Transaction trAdding = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
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
                            trAdding.Commit();
                        }
                    }
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

                foreach (SelectedObject acSSObj in acSSet)
                {
                    if (acSSObj != null)
                    {
                        using (Transaction trAdding = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
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
                            trAdding.Commit();
                        }
                    }
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

                foreach (SelectedObject acSSObj in acSSet)
                {
                    if (acSSObj != null)
                    {


                        using (Transaction trAdding = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
                        {
                            ObjectId objId = acSSObj.ObjectId;

                            //Проверка на dim
                            if (trAdding.GetObject(objId, OpenMode.ForRead) is Dimension)
                            {

                                Dimension entity = trAdding.GetObject(objId, OpenMode.ForRead) as Dimension;
                                tempListHandle.Add(entity.ObjectId.Handle);
                                tempObjectID.Add(entity.ObjectId);

                                string resultMeasurement = entity.FormatMeasurement(entity.Measurement, "");


                                if (!string.IsNullOrEmpty(entity.Prefix) | !string.IsNullOrEmpty(entity.Suffix))
                                {
                                    string Prefix = " ";
                                    string Suffix = " ";

                                    if (!string.IsNullOrEmpty(entity.Prefix))
                                    {
                                        Prefix = entity.Prefix;
                                    }
                                    if (!string.IsNullOrEmpty(entity.Suffix))
                                    {
                                        Suffix = entity.Suffix;
                                    }

                                    //ed.WriteMessage("DimensionText  Suff isNull ?: "+ string.IsNullOrEmpty(entity.Prefix)+" "+entity.Prefix);
                                    // ed.WriteMessage("DimensionText isPref isNull?: "+ string.IsNullOrEmpty(entity.Suffix)+ " "+entity.Suffix);

                                    resultMeasurement = resultMeasurement.Replace(Prefix, "").Replace(Suffix, "").Replace("\\A1;", "");

                                }
                                else
                                {
                                    resultMeasurement = entity.FormatMeasurement(entity.Measurement, "").Replace("\\A1;", "");

                                }



                                if (!string.IsNullOrEmpty(entity.DimensionText))
                                {
                                    //ed.WriteMessage("DimensionText: "+entity.DimensionText);
                                    //ed.WriteMessage("resultMeasurement: "+resultMeasurement );


                                    double doleValue = 0;
                                    bool isAdd = double.TryParse(entity.DimensionText.Trim().Replace(",", "."), out doleValue);

                                    if (isAdd)
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

                                    double temp;

                                    //Тут в чем то косяк в реплйсе

#if nanoCAD
                                    if (double.TryParse(resultMeasurement.Replace(".", ","), out temp))
#else
                                    if (double.TryParse(resultMeasurement.Replace(",", "."), out temp))
#endif

                                    {
                                        //ed.WriteMessage("temp: "+temp );
                                    }
                                    else
                                    {
                                        MyOpenDocument.ed.WriteMessage("Невозможно преобразовать строку в число.");
                                    }

                                    resultItem.result = resultItem.result + temp;
                                }



                                //resultItem.result = resultItem.result + Math.Round(entity.Measurement, entity.Dimdec);
                            }
                            trAdding.Commit();
                        }
                    }
                }
                resultItem.AllHandel = tempListHandle;
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




        public EntMtextOrDimToSumOrCount()
        {

            MyOpenDocument.ed = Application.DocumentManager.MdiActiveDocument.Editor;
            MyOpenDocument.doc = Application.DocumentManager.MdiActiveDocument;
            MyOpenDocument.dbCurrent = Application.DocumentManager.MdiActiveDocument.Database;

            this._tools = new Serialize(MyOpenDocument.doc, MyOpenDocument.dbCurrent, MyOpenDocument.ed);
            MyOpenDocument.ed.WriteMessage("Loading... EntMtextOrDimToSumOrCount | AeroHost 2025г.");
            MyOpenDocument.ed.WriteMessage("\n");
            MyOpenDocument.ed.WriteMessage("| йф - Сама считалка.");
            MyOpenDocument.ed.WriteMessage("| йфф - Восстановление набора по Handle. Долго восстанавливает при большом чертеже.");
            MyOpenDocument.ed.WriteMessage("| йффф - Восстановление набора по ObjectID. ТОЛЬКО ДЛЯ ТЕКУЩЕГО СЕАНСА. Восстаналивает быстро.");
            MyOpenDocument.ed.WriteMessage("| цф - Расворачивает профил линии в прямую с высотами.");
            MyOpenDocument.ed.WriteMessage("| цфф - Построить точку выстоной отметки интерполяцией имея две точки.");
            MyOpenDocument.ed.WriteMessage("| йц - Скрытие фона у mTexta и Выносок");
            MyOpenDocument.ed.WriteMessage("\n");

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





    }
}





