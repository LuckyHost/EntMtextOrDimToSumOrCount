
#region Namespaces


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;




#if nanoCAD
using Application = HostMgd.ApplicationServices.Application;
using HostMgd.ApplicationServices;
using Teigha.DatabaseServices;
using HostMgd.EditorInput;
using Teigha.Geometry;
using Teigha.Runtime;
using Exception = Teigha.Runtime.Exception;

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

        Document doc = Application.DocumentManager.MdiActiveDocument;
        Database dbCurrent = Application.DocumentManager.MdiActiveDocument.Database;
        private Editor ed;
        private Serialize _tools;





        [CommandMethod("йф", CommandFlags.UsePickSet |
                      CommandFlags.Redraw | CommandFlags.Modal)] // название команды, вызываемой в Autocad
        public void MakeSumOrCountCommand()

        {
            if (doc == null) { return; }
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
                ed.WriteMessage("\n\nПустой выбор.\n");
                return;
            }


            PromptEntityOptions item = new PromptEntityOptions("\nВыберите объект(Mtext) куда сохранить результат: ");
            PromptEntityResult perItem = ed.GetEntity(item);

            //Обновляем текст в Мтекст
            UpdateTextById(perItem.ObjectId, temp.result.ToString(), 256);

            string xmlData = _tools.SerializeToXml<ItemElement>(temp);
            _tools.SaveXmlToXrecord(xmlData, perItem.ObjectId, "Makarov.D_entMtextOrDimensionToSum");


            if (temp.ObjSelID.Count > 0)
            {
                SelectObjects(temp.ObjSelID);
                ed.WriteMessage("\n\n !!!!!!!!!!! \n\nЕсть фиктивные значения, я их все равно сложил, сейчас подсвечу. \n");
            }

        }

        [CommandMethod("йфф", CommandFlags.UsePickSet |
                       CommandFlags.Redraw | CommandFlags.Modal)] // название команды, вызываемой в Autocad
        public async void inDataSumm()

        {
            if (doc == null) return;

            try
            {
                PromptEntityOptions item = new PromptEntityOptions("\n Handl. Выберите объект(Mtext) что б вернуть выделение: \n");
                PromptEntityResult perItem = ed.GetEntity(item);

                if (perItem.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("Отмена");
                    return;

                }

                ItemElement selectionItem = _tools.ShowExtensionDictionaryContents<ItemElement>(perItem.ObjectId, "Makarov.D_entMtextOrDimensionToSum");
                if (selectionItem != null)
                {
                    ed.WriteMessage("Работаю асинхронно, засекаю время...");
                    List<ObjectId> tempList = new List<ObjectId>();
                    Stopwatch stopwatch = new Stopwatch();

                    ObjectId[] allentity;
                    using (Transaction tr = dbCurrent.TransactionManager.StartTransaction())
                    {
                        // Используем транзакцию для открытия таблицы объектов
                        BlockTable bt = tr.GetObject(dbCurrent.BlockTableId, OpenMode.ForRead) as BlockTable;
                        BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                        allentity = modelSpace.Cast<ObjectId>().ToArray();
                        tr.Commit();
                    }

                    using (Transaction tr = dbCurrent.TransactionManager.StartTransaction())
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
                        ed.WriteMessage("Прошло с момента операции:  " + stopwatch.Elapsed.TotalSeconds + " c.");
                        tr.Commit();
                        SelectObjects(tempList);
                    }

                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage(ex.ToString());
                return;
            }

        }

        [CommandMethod("йффф", CommandFlags.UsePickSet |
                      CommandFlags.Redraw | CommandFlags.Modal)] // название команды, вызываемой в Autocad
        public void inDataSummObjId()

        {
            if (doc == null) return;

            PromptEntityOptions item = new PromptEntityOptions("\n ObjectID Выберите объект(Mtext) что б вернуть выделение: \n");
            PromptEntityResult perItem = ed.GetEntity(item);
            ItemElement selectionItem = _tools.ShowExtensionDictionaryContents<ItemElement>(perItem.ObjectId, "Makarov.D_entMtextOrDimensionToSum");
            if (perItem.Status != PromptStatus.OK)
            {
                ed.WriteMessage("Отмена");
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
                ed.WriteMessage("Не могу найти по Object ID(использовать только в ТЕКУЩЕЙ СЕССИИ), возможно надо по Handel.");

                return;

            }

        }




        [CommandMethod("цф", CommandFlags.UsePickSet |
                      CommandFlags.Redraw | CommandFlags.Modal)] // название команды, вызываемой в Autocad
        public void DecomposePL()

        {
            if (doc == null) return;

            PromptEntityOptions item = new PromptEntityOptions("\n Выберите полилинию для разложения: \n");
            PromptEntityResult perItem = ed.GetEntity(item);
            if (perItem.Status != PromptStatus.OK)
            {
                ed.WriteMessage("Отмена");
                return;
            }
            // Начинаем транзакцию
            using (Transaction tr = dbCurrent.TransactionManager.StartTransaction())
            {

               
                    
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
                                                ed.WriteMessage($"\nВыбрана полилиния с {polyline.NumberOfVertices} вершинами.");

                            // Создаем новую полилинию для прямого пути
                            Polyline newPolyline = new Polyline();
                            double currentX = polyline.GetPoint2dAt(0).X; // Начальная координата X для новой полилинии
                            double currentY = polyline.GetPoint2dAt(0).Y; // Начальная координата X для новой полилинии
                            int index = 0;

                            // Проходим по каждому сегменту полилинии
                            for (int i = 0; i < polyline.NumberOfVertices - 1; i++)
                                        {
                                            // Получаем две точки: текущую и следующую вершины
                                            Point3d pt1 = polyline.GetPoint3dAt(i);
                                            Point3d pt2 = polyline.GetPoint3dAt(i + 1);

                                            // Вычисляем длину сегмента как расстояние между точками
                                            double segmentLength = pt1.DistanceTo(pt2);
                                            ed.WriteMessage($"\nДлина сегмента {i + 1}: {segmentLength}");

                                // Добавляем вершину в новую полилинию
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
                                            ed.WriteMessage($"\nДлина последнего сегмента: {segmentLength}");
                                        }


                            // Добавляем новую полилинию в чертеж
                            BlockTable bt = tr.GetObject(dbCurrent.BlockTableId, OpenMode.ForRead) as BlockTable;
                            BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                            btr.AppendEntity(newPolyline);
                            tr.AddNewlyCreatedDBObject(newPolyline, true);

                            ed.WriteMessage($"\nНовая полилиния построена с {newPolyline.NumberOfVertices} вершинами.");

                        
                        }
                        else if (ent is Polyline2d)
                                {
                                    Polyline2d polyline2d = ent as Polyline2d;
                                    ed.WriteMessage("\nВыбрана 2D полилиния.");
                                }
                                else
                                {
                                    ed.WriteMessage("\nВыбранный объект не является полилинией.");
                                }
                            }
                        }
                    

                    // Завершаем транзакцию
                    tr.Commit();
                
            }

        }



        private ItemElement getMext(IsCheck Is)
        {
            ItemElement resultItem = new ItemElement();
            List<Handle> tempListHandle = new List<Handle>();
            List<ObjectId> tempObjectID = new List<ObjectId>();


            PromptSelectionResult acSSPrompt = ed.GetSelection();

            if (acSSPrompt.Status == PromptStatus.OK)
            {
                SelectionSet acSSet = acSSPrompt.Value;

                foreach (SelectedObject acSSObj in acSSet)
                {
                    if (acSSObj != null)
                    {
                        using (Transaction trAdding = dbCurrent.TransactionManager.StartTransaction())
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
                                        ed.WriteMessage("\n\n Ты где-то ошибся, есть нечисловой текст \n Перепроверь, я тут подожду.");
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


            PromptSelectionResult acSSPrompt = ed.GetSelection();

            if (acSSPrompt.Status == PromptStatus.OK)
            {
                SelectionSet acSSet = acSSPrompt.Value;

                foreach (SelectedObject acSSObj in acSSet)
                {
                    if (acSSObj != null)
                    {
                        using (Transaction trAdding = dbCurrent.TransactionManager.StartTransaction())
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


            PromptSelectionResult acSSPrompt = ed.GetSelection();

            if (acSSPrompt.Status == PromptStatus.OK)
            {
                SelectionSet acSSet = acSSPrompt.Value;

                foreach (SelectedObject acSSObj in acSSet)
                {
                    if (acSSObj != null)
                    {


                        using (Transaction trAdding = dbCurrent.TransactionManager.StartTransaction())
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
                                        ed.WriteMessage("\n\n Ты где-то ошибся, есть нечисловой текст \n Перепроверь, я тут подожду.");
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
                                        ed.WriteMessage("Невозможно преобразовать строку в число.");
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

            PromptResult result = ed.GetKeywords(options);
            if (result.Status == PromptStatus.OK)
            {
                ed.WriteMessage("Вы выбрали : " + result.StringResult);

            }
            else
            {
                ed.WriteMessage("\n\nОтмена.\n");
                return null;
            }




            return result.StringResult;
        }


        public void UpdateTextById(ObjectId textId, string newText, int colorIndex)
        {

            using (Transaction tr = dbCurrent.TransactionManager.StartTransaction())
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
                        ed.WriteMessage("Unable to open MText with ObjectId: {0}\n", textId);
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
                ed.SetImpliedSelection(objectIds.ToArray());
            }

            catch (Exception ex)
            {
                ed.WriteMessage("Я думаю, скорее всего надо сделать восстановление по Handel.");
                return;
            }
        }





        public void ZoomToEntity(ObjectId entityId, double zoomPercent)
        {



            using (DocumentLock doclock = doc.LockDocument())
            {

                using (Transaction tr = dbCurrent.TransactionManager.StartTransaction())
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
                            ed.SetImpliedSelection(new ObjectId[] { entityId });
                            ed.SetCurrentView(view);
                            ed.CurrentUserCoordinateSystem = Matrix3d.Identity;
                        }
                    }

                    tr.Commit();
                }
            }
        }



        [CommandMethod("ListVisibleLayers")]
        public void DisplayVisibleLayersInViewport()
        {


            using (Transaction tr = dbCurrent.TransactionManager.StartTransaction())
            {
                // Определяем текущее пространство
                Layout currentLayout = null; // Инициализируем переменную текущего макета
                LayoutManager layoutManager = LayoutManager.Current;
                if (layoutManager != null)
                {
                    ObjectId currentLayoutId = layoutManager.GetLayoutId(layoutManager.CurrentLayout); // Получаем идентификатор текущего макета
                    if (!currentLayoutId.IsNull)
                    {
                        currentLayout = tr.GetObject(currentLayoutId, OpenMode.ForRead) as Layout; // Получаем сам объект макета
                    }
                }

                // Проверяем, что объект текущего макета успешно получен
                if (currentLayout != null)
                {
                    // Получаем имя текущего макета
                    string currentSpace = currentLayout.LayoutName;

                    // Создаем список для хранения имен видимых слоев
                    HashSet<string> visibleLayers = new HashSet<string>();

                    // Начинаем транзакцию
                   
                        // Открываем пространство модели
                        BlockTable bt = (BlockTable)tr.GetObject(dbCurrent.BlockTableId, OpenMode.ForRead);
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[currentSpace], OpenMode.ForRead);

                        // Проходимся по всем объектам в пространстве модели
                        foreach (ObjectId objId in btr)
                        {
                            Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                            if (ent != null)
                            {
                                // Получаем имя слоя объекта и добавляем его в список видимых слоев
                                string layerName = ent.Layer;
                                visibleLayers.Add(layerName);
                            }
                        }

                        // Завершаем транзакцию
                        tr.Commit();
                    

                    // Выводим имена видимых слоев в консоль
                    foreach (string layerName in visibleLayers)
                    {
                        ed.WriteMessage(layerName + "\n");
                    }
                }
                else
                {
                    // Обработка ситуации, когда не удалось получить текущий макет
                    // Например, выведем сообщение об ошибке
                    ed.WriteMessage("Не удалось получить текущий макет.");
                }
            }

        }


        public EntMtextOrDimToSumOrCount()
        {

            this.ed = Application.DocumentManager.MdiActiveDocument.Editor;
            this._tools = new Serialize(doc, dbCurrent, ed);
            ed.WriteMessage("Loading... EntMtextOrDimToSumOrCount | AeroHost 2024г.");
            ed.WriteMessage("\n");
            ed.WriteMessage("| йф - Сама считалка.");
            ed.WriteMessage("| йфф - Восстановление набора по Handle. Долго восстанавливает при большом чертеже.");
            ed.WriteMessage("| йффф - Восстановление набора по ObjectID. ТОЛЬКО ДЛЯ ТЕКУЩЕГО СЕАНСА. Восстаналивает быстро.");
            ed.WriteMessage("\n");

        }





    }
}





