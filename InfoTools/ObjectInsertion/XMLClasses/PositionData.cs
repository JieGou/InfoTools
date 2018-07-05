﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Civil3DInfoTools.ObjectInsertion.XMLClasses
{
    public class PositionData
    {
        [XmlArray("ObjectPositions"), XmlArrayItem("ObjectPosition")]
        public List<ObjectPosition> ObjectPositions { get; set; } = new List<ObjectPosition>();
    }
}