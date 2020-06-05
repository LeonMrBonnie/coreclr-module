using System;
using System.Threading.Tasks;
using AltV.Net.Elements.Entities;
using AltV.Net.Elements.Pools;
using AltV.Net.Elements.Refs;

namespace AltV.Net.Async.Elements.Pools
{
    public class AsyncPlayerPool : AsyncEntityPool<IPlayer>
    {
        public AsyncPlayerPool(IEntityFactory<IPlayer> entityFactory) : base(entityFactory)
        {
        }

        public override ushort GetId(IntPtr entityPointer)
        {
            return AltAsync.Do(() => Player.GetId(entityPointer)).Result;
        }

        public override async Task ForEach(IAsyncBaseObjectCallback<IPlayer> asyncBaseObjectCallback)
        {
            foreach (var entity in GetAllEntities())
            {
                using var entityRef = new PlayerRef(entity);
                if (entityRef.Exists)
                {
                    await asyncBaseObjectCallback.OnBaseObject(entity);
                }
            }
        }

        public override void ForEach(IBaseObjectCallback<IPlayer> baseObjectCallback)
        {
            foreach (var entity in GetAllEntities())
            {
                using var entityRef = new PlayerRef(entity);
                if (entityRef.Exists)
                {
                    baseObjectCallback.OnBaseObject(entity);
                }
            }
        }
    }
}