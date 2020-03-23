﻿using System;
using appbox.Serialization;

namespace appbox.Models
{
    public sealed class EnumModelItem : IBinSerializable
    {
        #region ====Fields & Properties====
        public string Name { get; private set; }

        public int Value { get; private set; }

        public string Comment { get; set; }
        #endregion

        #region ====Ctor====
        internal EnumModelItem() { }

        internal EnumModelItem(string name, int value)
        {
            Name = name;
            Value = value;
        }
        #endregion

        #region ====Serialization====
        void IBinSerializable.WriteObject(BinSerializer bs)
        {
            bs.Write(Name, 1);
            bs.Write(Value, 2);
            if (!string.IsNullOrEmpty(Comment))
                bs.Write(Comment, 3);

            bs.Write(0u);
        }

        void IBinSerializable.ReadObject(BinSerializer bs)
        {
            uint propIndex;
            do
            {
                propIndex = bs.ReadUInt32();
                switch (propIndex)
                {
                    case 1: Name = bs.ReadString(); break;
                    case 2: Value = bs.ReadInt32(); break;
                    case 3: Comment = bs.ReadString(); break;
                    case 0: break;
                    default: throw new Exception($"Deserialize_ObjectUnknownFieldIndex: {GetType().Name}");
                }
            } while (propIndex != 0);
        }
        #endregion
    }
}
