using System;
using System.Runtime.InteropServices;
using AltV.Net.Data;
using AltV.Net.Elements.Args;
using AltV.Net.Native;

namespace AltV.Net.Elements.Entities
{
    public class ColShape : WorldObject, IColShape
    {
        public override Position Position
        {
            get
            {
                CheckIfEntityExists();
                var position = Position.Zero;
                AltNative.ColShape.ColShape_GetPosition(NativePointer, ref position);
                return position;
            }
            set
            {
                CheckIfEntityExists();
                AltNative.ColShape.ColShape_SetPosition(NativePointer, value);
            }
        }

        public override int Dimension
        {
            get
            {
                CheckIfEntityExists();
                return AltNative.ColShape.ColShape_GetDimension(NativePointer);
            }
            set
            {
                CheckIfEntityExists();
                AltNative.ColShape.ColShape_SetDimension(NativePointer, value);
            }
        }

        public override void GetMetaData(string key, out MValueConst value)
        {
            var stringPtr = AltNative.StringUtils.StringToHGlobalUtf8(key);
            value = new MValueConst(AltNative.ColShape.ColShape_GetMetaData(NativePointer, stringPtr));
            Marshal.FreeHGlobal(stringPtr);
        }

        public override void SetMetaData(string key, in MValueConst value)
        {
            var stringPtr = AltNative.StringUtils.StringToHGlobalUtf8(key);
            AltNative.ColShape.ColShape_SetMetaData(NativePointer, stringPtr, value.nativePointer);
            Marshal.FreeHGlobal(stringPtr);
        }

        public ColShapeType ColShapeType
        {
            get
            {
                CheckIfEntityExists();
                return AltNative.ColShape.ColShape_GetColShapeType(NativePointer);
            }
        }

        public ColShape(IntPtr nativePointer) : base(nativePointer, BaseObjectType.ColShape)
        {
        }

        public bool IsEntityIn(IEntity entity)
        {
            CheckIfEntityExists();
            entity.CheckIfEntityExists();

            switch (entity)
            {
                case IPlayer player:
                    return AltNative.ColShape.ColShape_IsPlayerIn(NativePointer, player.NativePointer);
                case IVehicle vehicle:
                    return AltNative.ColShape.ColShape_IsVehicleIn(NativePointer, vehicle.NativePointer);
                default:
                    return false;
            }
        }

        public void Remove()
        {
            Alt.RemoveColShape(this);
        }
        
        protected override void InternalAddRef()
        {
            AltNative.ColShape.ColShape_AddRef(NativePointer);
        }

        protected override void InternalRemoveRef()
        {
            AltNative.ColShape.ColShape_RemoveRef(NativePointer);
        }
    }
}