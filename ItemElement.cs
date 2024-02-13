
#region Namespaces


using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;



#if nanoCAD
using Teigha.DatabaseServices;

#else
using Autodesk.AutoCAD.DatabaseServices;

#endif

#endregion Namespaces

namespace ent
{
    [Serializable]
    public class ItemElement
    {
        private List<Handle> _AllHandel;
		private List<ObjectId> _AllObjectID;
        private List<string> _SerializedAllObjectID;
        
        
        [XmlElement("result")]
        public double result { get; set; }
        
        [XmlIgnore]
        public List<Handle> AllHandel
        {
            get { return _AllHandel; }
            set
            {
                _AllHandel = value;
                SerializedAllHandel = value.Select(objId => objId.ToString()).ToList();
            }
        }
        
      
		[XmlElement("AllHandel")]
        public List<string> SerializedAllHandel  { get; set; }
       	
        [XmlIgnore]
         public List<ObjectId> ObjSelID  { get; set; }
        
        
          [XmlIgnore]
        public List<ObjectId> AllObjectID 
   		 {		
        	get{ return _AllObjectID;}
        	
        	set{ _AllObjectID = value;
        		SerializedAllObjectID = value.Select(objId => long.Parse( objId.ToString())).ToList();
        		}
        	
        }
        
        
        [XmlElement("AllObjectID")]
        public List<long> SerializedAllObjectID { get; set; }

        


        public ItemElement()
        {
            result = 0.0;
            AllHandel = new List<Handle>();
            AllObjectID = new List<ObjectId>();
         	ObjSelID= new List<ObjectId>();
         	SerializedAllObjectID= new List<long>();
        }

    }
}
