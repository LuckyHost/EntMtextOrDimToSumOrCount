
#region Namespaces






#if nanoCAD
using HostMgd.ApplicationServices;
using HostMgd.EditorInput;
using Teigha.DatabaseServices;
using Color = Teigha.Colors.Color;
using System.Collections.Generic;
using Teigha.Runtime;

#else
using Color = Autodesk.AutoCAD.Colors.Color;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System.Collections.Generic;
using Autodesk.AutoCAD.Runtime;



#endif

#endregion Namespaces

namespace EntMtextOrDimToSumOrCount
{
    public static class Layer
    {

        //Создание слоев
        public static void creatLayer(string Name, byte ColorR, byte ColorG, byte ColorB)
        {

            using (DocumentLock docloc = MyOpenDocument.doc.LockDocument())
            {
                using (Transaction trAdding = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
                {

                    LayerTable layerTable = trAdding.GetObject(MyOpenDocument.dbCurrent.LayerTableId, OpenMode.ForWrite) as LayerTable;

                    if (layerTable.Has(Name))
                    {
                        MyOpenDocument.ed.WriteMessage($"\nСлой уже существует: \"{Name}\" ");
                    }
                    if (!layerTable.Has(Name))
                    {
                        // Создание слоя
                        LayerTableRecord acLyrTblRec = new LayerTableRecord();
                        acLyrTblRec.Name = Name;
                        acLyrTblRec.Color = Color.FromRgb(ColorR, ColorG, ColorB);
                        layerTable.UpgradeOpen();
                        ObjectId acObjId = layerTable.Add(acLyrTblRec);
                        trAdding.AddNewlyCreatedDBObject(acLyrTblRec, true);
                        MyOpenDocument.ed.WriteMessage($"\nСлой создан: \"{Name}\"  ! Не удаляйте данный слой !");
                    }
                    trAdding.Commit();
                }
            }


        }

   
        public static void deleteObjectsOnLayer(string layerNameDelete, bool isLayerErase = true)
        {
            Editor ed = MyOpenDocument.ed;
            Database dbCurrent = MyOpenDocument.dbCurrent;
            Document doc = MyOpenDocument.doc;

            // Словарь для хранения состояния блокировки каждого слоя
            Dictionary<string, bool> layerLockStates = new Dictionary<string, bool>();

            using (Transaction tr = dbCurrent.TransactionManager.StartTransaction())
            {
                LayerTable lt = tr.GetObject(dbCurrent.LayerTableId, OpenMode.ForRead) as LayerTable;

                if (!lt.Has(layerNameDelete))
                {
                    ed.WriteMessage($"\nСлой \"{layerNameDelete}\" не найден.");
                    return;
                }

                // Сохраняем текущее состояние блокировки и разблокируем все слои
                foreach (ObjectId layerId in lt)
                {
                    LayerTableRecord ltr = tr.GetObject(layerId, OpenMode.ForWrite) as LayerTableRecord;

                    // Сохраняем состояние блокировки слоя
                    layerLockStates[ltr.Name] = ltr.IsLocked;

                    // Разблокируем слой
                    ltr.IsLocked = false;
                }

                if (lt.Has(layerNameDelete))
                {
                    // Получаем объект LayerTableRecord (слой)
                    LayerTableRecord ltr = tr.GetObject(lt[layerNameDelete], OpenMode.ForWrite) as LayerTableRecord;

                    // Поиск всех объектов на этом слое и удаление их
                    BlockTable bt = tr.GetObject(dbCurrent.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    foreach (ObjectId id in btr)
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                        if (ent != null && ent.Layer == layerNameDelete)
                        {
                            try
                            {
                                ent.Erase();
                            }
                            catch (Exception ex)
                            {
                                MyOpenDocument.ed.WriteMessage($"\nОшибка удаления объекта на слое \"{layerNameDelete}\": {ex.Message}");
                            }
                        }
                    }

                    // Проверка, что слой не является текущим слоем
                    if (dbCurrent.Clayer == ltr.ObjectId)
                    {
                        dbCurrent.Clayer = dbCurrent.LayerZero; // Установить текущий слой на 0-й слой
                    }

                    if (isLayerErase)
                    {
                        // Удаление слоя
                        ltr.Erase();
                        MyOpenDocument.ed.WriteMessage($"\nСлой \"{layerNameDelete}\" удален");
                    }

                }
                tr.Commit();
            }

            // Восстанавливаем исходное состояние блокировки слоев
            using (Transaction tr = dbCurrent.TransactionManager.StartTransaction())
            {
                LayerTable lt = tr.GetObject(dbCurrent.LayerTableId, OpenMode.ForRead) as LayerTable;

                foreach (ObjectId layerId in lt)
                {
                    LayerTableRecord ltr = tr.GetObject(layerId, OpenMode.ForWrite) as LayerTableRecord;

                    // Восстанавливаем состояние блокировки слоя
                    if (layerLockStates.ContainsKey(ltr.Name))
                    {
                        ltr.IsLocked = layerLockStates[ltr.Name];
                    }
                }

                tr.Commit();
            }
        }

    }
}
