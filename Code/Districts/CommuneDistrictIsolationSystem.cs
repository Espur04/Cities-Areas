using Game;
using Game.Areas;
using Game.Common;
using Game.Policies;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace Communes
{

public partial class CommuneDistrictIsolationSystem : GameSystemBase
    {
        private EntityQuery m_CommuneAreas;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_CommuneAreas = GetEntityQuery(ComponentType.ReadOnly<Commune>(),
                ComponentType.ReadOnly<Area>(), ComponentType.Exclude<Deleted>());
        }

        protected override void OnUpdate()
        {
            NativeArray<Entity> entities = m_CommuneAreas.ToEntityArray(Allocator.Temp);
            int detached = 0;
            try
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity entity = entities[i];
                    bool changed = false;
                    if (EntityManager.HasComponent<District>(entity))
                    {
                        EntityManager.RemoveComponent<District>(entity);
                        changed = true;
                    }
                    if (EntityManager.HasComponent<DistrictData>(entity))
                    {
                        EntityManager.RemoveComponent<DistrictData>(entity);
                        changed = true;
                    }
                    if (EntityManager.HasBuffer<DistrictModifier>(entity))
                    {
                        EntityManager.RemoveComponent<DistrictModifier>(entity);
                        changed = true;
                    }
                    if (EntityManager.HasBuffer<Policy>(entity))
                    {
                        EntityManager.RemoveComponent<Policy>(entity);
                        changed = true;
                    }

                    if (changed)
                    {
                        if (!EntityManager.HasComponent<Updated>(entity))
                            EntityManager.AddComponent<Updated>(entity);
                        detached++;
                    }
                }
            }
            finally
            {
                entities.Dispose();
            }

            if (detached > 0)
                Mod.log.Info($"[Communes][Isolation] {detached} ancienne(s) commune(s) détachée(s) des districts natifs.");
        }
    }
}
