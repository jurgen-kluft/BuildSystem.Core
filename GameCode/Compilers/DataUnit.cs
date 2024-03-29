﻿using System;
using System.Reflection;
using System.Collections.Generic;
using GameCore;

namespace GameData
{
    public sealed class DataUnit : IDataUnit
    {
        public string MemberName { get; set; }
        public string DataUnitPath { get; set; }

        public EDataUnit Mode { get; set; }

        public DataUnit(string membername, string unitpath) : this(membername, unitpath, EDataUnit.External)
        {
        }

        public DataUnit(string membername, string unitpath, EDataUnit dataUnit)
        {
            MemberName = membername;
            DataUnitPath = unitpath;
            UnitType = dataUnit;
        }

        public EDataUnit UnitType { get; set; }

        public object extobject
        {
            get
            {
                return null;
            }
        }

        public string name { get { return MemberName; } }
        public object value { get; set; }
    }
}

