﻿using System;
using appbox.Data;
using appbox.Serialization;
using Newtonsoft.Json;

namespace appbox.Models
{
    public sealed class DataFieldModel : EntityMemberModel
    {
        #region ====Fields & Properties====
        public override EntityMemberType Type => EntityMemberType.DataField;

        /// <summary>
        /// 字段类型
        /// </summary>
        /// <remarks>set for design must call OnDataTypeChanged</remarks>
        public EntityFieldType DataType { get; internal set; }

        /// <summary>
        /// 是否引用外键
        /// </summary>
        internal bool IsForeignKey { get; private set; }

        internal bool IsDataTypeChanged { get; private set; }

        internal bool IsDefaultValueChanged { get; private set; }

        /// <summary>
        /// 如果DataType = Enum,则必须设置相应的EnumModel.ModelId
        /// </summary>
        /// <remarks>set for design must call OnPropertyChanged</remarks>
        internal ulong EnumModelId { get; set; }

        /// <summary>
        /// 仅用于Sql存储设置字符串最大长度(0=无限制)或Decimal整数部分长度
        /// </summary>
        /// <remarks>set for design must call OnDataTypeChanged</remarks>
        public uint Length { get; internal set; }

        /// <summary>
        /// 仅用于Sql存储设置Decimal小数部分长度
        /// </summary>
        /// <remarks>set for design must call OnDataTypeChanged</remarks>
        public uint Decimals { get; internal set; }

        /// <summary>
        /// 非空的默认值
        /// </summary>
        /// <remarks>set for design must call OnDefaultValueChanged</remarks>
        internal EntityMember? DefaultValue { get; set; }

        /// <summary>
        /// 保留用于根据规则生成Sql列的名称, eg:相同前缀、命名规则等
        /// </summary>
        internal string SqlColName => Name;

        internal string SqlColOriginalName => OriginalName;

        /// <summary>
        /// 是否系统存储的分区键
        /// </summary>
        internal bool IsPartitionKey
        {
            get
            {
                if (Owner.SysStoreOptions != null && Owner.SysStoreOptions.HasPartitionKeys)
                {
                    for (int i = 0; i < Owner.SysStoreOptions.PartitionKeys.Length; i++)
                    {
                        if (Owner.SysStoreOptions.PartitionKeys[i].MemberId == MemberId)
                            return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// 是否Sql存储的主键
        /// </summary>
        internal bool IsPrimaryKey
        {
            get
            {
                if (Owner.SqlStoreOptions != null && Owner.SqlStoreOptions.HasPrimaryKeys)
                {
                    for (int i = 0; i < Owner.SqlStoreOptions.PrimaryKeys.Count; i++)
                    {
                        if (Owner.SqlStoreOptions.PrimaryKeys[i].MemberId == MemberId)
                            return true;
                    }
                }
                return false;
            }
        }
        #endregion

        #region ====Ctor====
        internal DataFieldModel() { }

        internal DataFieldModel(EntityModel owner, string name,
            EntityFieldType dataType, bool isFK = false) : base(owner, name)
        {
            DataType = dataType;
            IsForeignKey = isFK;
        }
        #endregion

        #region ====Runtime Methods====
        internal override void InitMemberInstance(Entity owner, ref EntityMember member)
        {
            member.Id = MemberId;
            member.MemberType = EntityMemberType.DataField;
            member.ValueType = DataType;
            member.Flag.IsForeignKey = IsForeignKey;
            member.Flag.AllowNull = AllowNull;
            //处理默认值
            if (DefaultValue.HasValue)
            {
                member.GuidValue = DefaultValue.Value.GuidValue;
                member.ObjectValue = DefaultValue.Value.ObjectValue;
                member.Flag.HasValue = true;
            }
            else
            {
                member.Flag.HasValue = !AllowNull; //必须设置
            }
        }
        #endregion

        #region ====Design Methods====
        internal void SetDefaultValue(string value)
        {
            if (AllowNull)
                throw new NotSupportedException("Can't set default value when allow null");

            var v = new EntityMember();
            v.Id = MemberId;
            v.MemberType = EntityMemberType.DataField;
            v.ValueType = DataType;
            v.Flag.AllowNull = AllowNull;
            v.Flag.HasValue = true;

            switch (DataType)
            {
                case EntityFieldType.String:
                    v.ObjectValue = value; break;
                case EntityFieldType.DateTime:
                    v.DateTimeValue = DateTime.Parse(value); break;
                case EntityFieldType.Int32:
                    v.Int32Value = int.Parse(value); break;
                case EntityFieldType.Decimal:
                    v.DecimalValue = decimal.Parse(value); break;
                case EntityFieldType.Float:
                    v.FloatValue = float.Parse(value); break;
                case EntityFieldType.Double:
                    v.DoubleValue = double.Parse(value); break;
                case EntityFieldType.Boolean:
                    v.BooleanValue = bool.Parse(value); break;
                case EntityFieldType.Guid:
                    v.GuidValue = Guid.Parse(value); break;
                default:
                    throw new NotImplementedException();
            }

            DefaultValue = v;
            OnDefaultValueChanged();
        }

        internal void OnDataTypeChanged()
        {
            if (PersistentState == PersistentState.Unchanged)
            {
                IsDataTypeChanged = true;
                OnPropertyChanged();
            }
        }

        internal void OnDefaultValueChanged()
        {
            if (PersistentState == PersistentState.Unchanged)
            {
                IsDefaultValueChanged = true;
                OnPropertyChanged();
            }
        }
        #endregion

        #region ====Serialization====
        public override void WriteObject(BinSerializer bs)
        {
            base.WriteObject(bs);

            bs.Write((byte)DataType, 1);
            bs.Write(IsForeignKey, 2);
            if (DataType == EntityFieldType.Enum)
                bs.Write(EnumModelId, 3);
            else if (DataType == EntityFieldType.String)
                bs.Write(Length, 5);
            else if (DataType == EntityFieldType.Decimal)
            {
                bs.Write(Length, 5);
                bs.Write(Decimals, 6);
            }

            if (DefaultValue.HasValue)
            {
                bs.Write((uint)4);
                DefaultValue.Value.Write(bs);
            }

            bs.Write(IsDataTypeChanged, 7);
            bs.Write(IsDefaultValueChanged, 8);

            bs.Write(0u);
        }

        public override void ReadObject(BinSerializer bs)
        {
            base.ReadObject(bs);

            uint propIndex;
            do
            {
                propIndex = bs.ReadUInt32();
                switch (propIndex)
                {
                    case 1: DataType = (EntityFieldType)bs.ReadByte(); break;
                    case 2: IsForeignKey = bs.ReadBoolean(); break;
                    case 3: EnumModelId = bs.ReadUInt64(); break;
                    case 4:
                        {
                            var dv = new EntityMember();
                            dv.Read(bs);
                            DefaultValue = dv; //Do not use DefaultValue.Value.Read
                            break;
                        }
                    case 5: Length = bs.ReadUInt32(); break;
                    case 6: Decimals = bs.ReadUInt32(); break;
                    case 7: IsDataTypeChanged = bs.ReadBoolean(); break;
                    case 8: IsDefaultValueChanged = bs.ReadBoolean(); break;
                    case 0: break;
                    default: throw new Exception($"Deserialize_ObjectUnknownFieldIndex: {GetType().Name}");
                }
            } while (propIndex != 0);
        }

        protected override void WriteMembers(JsonTextWriter writer, WritedObjects objrefs)
        {
            writer.WritePropertyName(nameof(DataType));
            writer.WriteValue((int)DataType);

            if (DataType == EntityFieldType.Enum)
            {
                writer.WritePropertyName(nameof(EnumModelId));
                writer.WriteValue(EnumModelId);
            }
            else if (DataType == EntityFieldType.String)
            {
                writer.WritePropertyName(nameof(Length));
                writer.WriteValue(Length);
            }
            else if (DataType == EntityFieldType.Decimal)
            {
                writer.WritePropertyName(nameof(Length));
                writer.WriteValue(Length);
                writer.WritePropertyName(nameof(Decimals));
                writer.WriteValue(Decimals);
            }
        }
        #endregion
    }
}
