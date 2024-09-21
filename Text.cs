
#region Namespaces
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;






#if nanoCAD
using HostMgd.ApplicationServices;
using HostMgd.EditorInput;
using Teigha.DatabaseServices;
using Teigha.Geometry;
using Teigha.Colors;


#else
using Color = Autodesk.AutoCAD.Colors.Color;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;


#endif

#endregion Namespaces

namespace EntMtextOrDimToSumOrCount
{
    
    public static class Text
    {


        //Функция создания Мтекста
        static public void creatText(string nameSearchLayer,Point2d point, string text, string sizeText, short color, int difPosishion)
        {


            using (DocumentLock docloc = MyOpenDocument.doc.LockDocument())
            {

                using (Transaction trAdding = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
                {

                    double size;
                    Double.TryParse(sizeText, out size);

                    // Ищу на какой слой закинуть
                    LayerTable acLyrTbl = trAdding.GetObject(MyOpenDocument.dbCurrent.LayerTableId, OpenMode.ForRead) as LayerTable;

                    ObjectId acLyrId = ObjectId.Null;
                    if (acLyrTbl.Has(nameSearchLayer))
                    {
                        acLyrId = acLyrTbl[nameSearchLayer];
                    }

                    // что б стащить цвет
                    // LayerTableRecord acLyrTblRec = trAdding.GetObject(acLyrId, OpenMode.ForRead) as LayerTableRecord;

                    // Для того что бы закинуть текст
                    BlockTable acBlkTbl = trAdding.GetObject(MyOpenDocument.dbCurrent.BlockTableId, OpenMode.ForRead) as BlockTable;

                    BlockTableRecord acBlkTblRec = trAdding.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    // Характеристики текста
                    MText acMText = new MText();
                    //acMText.Color = acLyrTblRec.Color; //Цвето по слою принудительно
                    acMText.Color = Color.FromColorIndex(ColorMethod.ByAci, color);
                    acMText.Contents = text;
                    acMText.Location = new Point3d(point.X, point.Y + difPosishion, 0);
                    acMText.TextHeight = size; //Размер шрифта было size
                                               //acMText.Height = 5; //Высота чего ?
                    acMText.LayerId = acLyrId;
                    acMText.Attachment = AttachmentPoint.MiddleCenter; //Центровка текста
                    acBlkTblRec.AppendEntity(acMText);
                    trAdding.AddNewlyCreatedDBObject(acMText, true);

                    trAdding.Commit();
                }
            }


        }

      /*

        public static void updateTextById(ObjectId textId, string newText, int colorIndex)
        {

            if (textId == null | newText == null)
            { return; }
            using (DocumentLock doclock = MyOpenDocument.doc.LockDocument())
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
                            MyOpenDocument.ed.WriteMessage("Unable to open MText with ObjectId\n");
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
        }

        public static int getColorMtext()
        {
            int colorIndex = 0;
            using (DocumentLock doclock = MyOpenDocument.doc.LockDocument())
            {
                using (Transaction tr = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
                {
                    MText mtext = tr.GetObject(itemPoint.IDText, OpenMode.ForRead) as MText;

                    if (mtext != null)
                    {
                        // Получение индекса цвета Mtext
                        colorIndex = mtext.ColorIndex;


                    }
                    tr.Commit();
                }
            }
            return colorIndex;



        }

        //Создает текст вершин
        public static string creatPromptKeywordOptions(string textName, List<string> listOptions, int defaultOptions)
        {



            //Для Acad, если пробел, он берет только первую часть 
            List<string> modifiedListOptions = listOptions.Select(option => option.Replace(" ", "_")).ToList();


            PromptKeywordOptions options = new PromptKeywordOptions(textName);

            foreach (string itemString in modifiedListOptions)
            {
                options.Keywords.Add(itemString);
            }
            options.Keywords.Default = modifiedListOptions[defaultOptions - 1]; // если сам, то -1

            PromptResult result = MyOpenDocument.ed.GetKeywords(options);
            if (result.Status == PromptStatus.OK)
            {
                string selectedKeyword = result.StringResult.Replace("_", " ");
                MyOpenDocument.ed.WriteMessage("\n\nВы выбрали : " + selectedKeyword + "\n\n");
                return selectedKeyword;
            }

            if (result.Status == PromptStatus.Cancel)
            {
                //Токен отмены
                MyOpenDocument.cts.Cancel();
                MyOpenDocument.ed.WriteMessage("ОТМЕНЕНО");
            }
            return null;
        }

        public static string getMTextContent(ObjectId mTextId)
        {
            using (Transaction tr = MyOpenDocument.dbCurrent.TransactionManager.StartTransaction())
            {
                // Открытие выбранного объекта MText для чтения
                MText mText = tr.GetObject(mTextId, OpenMode.ForRead) as MText;

                if (mText != null)
                {
                    // Вывод содержимого MText
                    MyOpenDocument.ed.WriteMessage("\nСодержимое MText: " + mText.Contents);
                    return mText.Contents;
                }
                else { return null; }

                // Не нужно коммитить транзакцию, так как мы только читаем данные

            }
        }*/



    } 
}
