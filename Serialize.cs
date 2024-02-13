
#region Namespaces


using System;
using System.IO;
using System.Xml.Serialization;



#if nanoCAD
using HostMgd.ApplicationServices;
using Teigha.DatabaseServices;
using HostMgd.EditorInput;
using Exception = Teigha.Runtime.Exception;

#else
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Exception = Autodesk.AutoCAD.Runtime.Exception;

#endif

#endregion Namespaces




namespace ent
{
    internal class Serialize
    {
         Document doc;
         Database dbCurrent;
         Editor ed;

        public T ShowExtensionDictionaryContents<T>(ObjectId entityId, string nameDictionary)
        {
            using (DocumentLock doclock = doc.LockDocument())
            {
                using (Transaction tr = dbCurrent.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // Открываем объект для чтения.
                        Entity entity = tr.GetObject(entityId, OpenMode.ForRead) as Entity;

                        if (entity != null && entity.ExtensionDictionary != ObjectId.Null)
                        {
                            DBDictionary extDict = tr.GetObject(entity.ExtensionDictionary, OpenMode.ForRead) as DBDictionary;

                            if (extDict != null && extDict.Contains(nameDictionary))
                            {
                                ObjectId entryId = extDict.GetAt(nameDictionary);

                                if (!entryId.IsNull)
                                {
                                    DBObject entryObj = tr.GetObject(entryId, OpenMode.ForRead);

                                    Xrecord xRecord = entryObj as Xrecord;

                                    if (xRecord != null)
                                    {
                                        //ed.WriteMessage("ExtensionDictionary contents for entity with ObjectId {0}:\n", entityId.Handle);
                                        /*
                                        // Получаем данные из Xrecord
                                        ResultBuffer data = xRecord.Data;
                                        foreach (TypedValue value in data)
                                        {
                                            ed.WriteMessage("TypedValue: {0}\n", value.Value);
                                        } */


                                        return DeserializeFromXrecord<T>(xRecord);
                                    }
                                    else
                                    {
                                        ed.WriteMessage("\n The entry with key" + nameDictionary + " is not an Xrecord.\n");

                                    }
                                }
                                else
                                {
                                    ed.WriteMessage(" \n Entry with key" + nameDictionary + " not found in ExtensionDictionary.\n");


                                }
                            }
                            else
                            {
                                ed.WriteMessage("\n ExtensionDictionary with key " + nameDictionary + " not found.\n");


                            }
                        }
                        else
                        {
                            ed.WriteMessage(" \n Не нашел значения из " + nameDictionary + " :(");
                            //ed.WriteMessage("Entity is null or does not have an ExtensionDictionary.\n");

                        }
                        //Если пусто
                        return default(T);

                    }

                    catch (System.Exception ex)
                    {
                        ed.WriteMessage("Error: {0}\n", ex.Message);

                        return default(T);
                    }
                    finally
                    {
                        tr.Dispose();

                    }
                }
            }
        }


        public T DeserializeFromXrecord<T>(Xrecord xRecord)
        {
            try
            {
                if (xRecord == null)
                    throw new ArgumentNullException("xRecord");
                ResultBuffer data = xRecord.Data;
                if (data == null)
                    throw new ArgumentException("Xrecord does not contain valid data.", "xRecord");
                TypedValue[] values = data.AsArray();
                if (values.Length == 1 && values[0].TypeCode == (int)DxfCode.Text)
                {
                    string xmlData = values[0].Value.ToString();
                    return DeserializeFromXml<T>(xmlData);
                }
                else
                {
                    throw new ArgumentException("Unexpected data format in Xrecord.", "xRecord");
                }
            }
            catch (InvalidOperationException ex)
            {
                // Обработка исключения или вывод информации о нем
                ed.WriteMessage("Ошибка десериализации: " + ex.Message);
                ed.WriteMessage("StackTrace: " + ex.StackTrace);
                return default(T);
            }
        }


        public T DeserializeFromXml<T>(string xmlData)
        {
            if (string.IsNullOrEmpty(xmlData))
                throw new ArgumentNullException("xmlData");
            try
            {

                XmlSerializer serializer = new XmlSerializer(typeof(T));
                using (StringReader reader = new StringReader(xmlData))
                {
                    return (T)serializer.Deserialize(reader);
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage("Error during deserialization: " + ex.Message);
                // Обработайте ошибку в соответствии с вашими потребностями
                throw;
            }
        }


        public string SerializeToXml<T>(T obj)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                StringWriter writer = new StringWriter();
                serializer.Serialize(writer, obj);

                // Указываем путь к файлу на рабочем столе
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string filePath = Path.Combine(desktopPath, "output.xml");

                // Записываем XML-строку в файл
                //File.WriteAllText(filePath, writer.ToString());

                return writer.ToString();
            }
            catch (Teigha.Runtime.Exception ex)
            {
                Console.WriteLine("Ошибка при сериализации: {ex.Message}");
                return null;
            }

        }
        public void SaveXmlToXrecord(string xmlData, ObjectId entityId, string nameDictionary)
        {
            using (DocumentLock doclock = doc.LockDocument())
            {
                using (Transaction tr = dbCurrent.TransactionManager.StartTransaction())
                {
                    try
                    {
                        Entity entity = tr.GetObject(entityId, OpenMode.ForWrite) as Entity;

                        if (entity != null)
                        {
                            if (entity.ExtensionDictionary == ObjectId.Null)
                            {
                                // Если у сущности еще нет словаря, создаем его
                                entity.CreateExtensionDictionary();
                            }

                            // Получение словаря
                            DBDictionary extDict = tr.GetObject(entity.ExtensionDictionary, OpenMode.ForWrite) as DBDictionary;


                            // Добавление или обновление записи в словаре
                            if (extDict.Contains(nameDictionary))
                            {

                                ObjectId entryIdq = extDict.GetAt(nameDictionary);
                                DBObject entryObj = tr.GetObject(entryIdq, OpenMode.ForWrite);
                                entryObj.UpgradeOpen();
                                entryObj.Erase(true);
                                entryObj.DowngradeOpen();

                            }

                            // Создание Xrecord
                            Xrecord xRecord = new Xrecord();
                            TypedValue tv = new TypedValue((int)DxfCode.Text, xmlData);
                            xRecord.Data = new ResultBuffer(tv);

                            ObjectId entryId = extDict.SetAt(nameDictionary, xRecord);
                            tr.AddNewlyCreatedDBObject(xRecord, true);
                            ed.WriteMessage("\n\nУспешно сохранено");


                        }

                        tr.Commit();
                    }

                    catch (Exception ex)
                    {
                        ed.WriteMessage("Ошибка: {0}\n", ex.Message);
                    }
                }
            }
        }
        public  Serialize(Document doc, Database dbCurrent, Editor ed)
        {
            this.doc = doc;
            this.dbCurrent = dbCurrent;
            this.ed = ed;
        }



    }

 
}
