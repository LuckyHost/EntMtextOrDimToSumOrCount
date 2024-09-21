using System.Threading;
#if nanoCAD
using HostMgd.ApplicationServices;
using HostMgd.EditorInput;
using Teigha.DatabaseServices;
#else
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

#endif
namespace EntMtextOrDimToSumOrCount
{
    internal class MyOpenDocument
    {
        public static Document doc;
        public static Database dbCurrent;
        public static Editor ed;
    }
}
