using Game.Areas;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Game.UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Communes
{

public partial class CommunesToolSystem : ToolBaseSystem
    {
        public override string toolID => "Communes Tool";

        private bool m_LoggedActivation;
        private bool m_LoggedNameState;
        private float m_NextNameProbeTime;

        protected override void OnCreate()
        {
            base.OnCreate();

            Mod.log.Info("[Communes] Système de diagnostic d'outil créé (mode passif : le tracé natif est piloté par AreaToolSystem).");
        }

public override PrefabBase GetPrefab()
        {
            return null;
        }

public override bool TrySetPrefab(PrefabBase prefab)
        {
            return false;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (!m_LoggedActivation && m_ToolSystem.activeTool is AreaToolSystem)
            {
                PrefabBase activePrefab = m_ToolSystem.activePrefab;
                if (activePrefab is CommuneAreaPrefab)
                {
                    m_LoggedActivation = true;
                    Mod.log.Info("[Communes] AreaToolSystem natif actif sur le prefab « CommuneAreaPrefab » " +
                                 "-> le tracé de polygone natif est disponible.");
                }
            }

if (!m_LoggedNameState && UnityEngine.Time.unscaledTime >= m_NextNameProbeTime)
            {
                m_NextNameProbeTime = UnityEngine.Time.unscaledTime + 5f;
                m_LoggedNameState = ProbeCommuneNames();
            }

            return inputDeps;
        }

private bool ProbeCommuneNames()
        {
            var em = EntityManager;
            var communes = GetEntityQuery(
                ComponentType.ReadOnly<Commune>(),
                ComponentType.ReadOnly<Area>(),
                ComponentType.ReadOnly<PrefabRef>());

            NativeArray<Entity> entities = communes.ToEntityArray(Allocator.Temp);
            try
            {
                if (entities.Length == 0)
                {
                    return false;
                }

                NameSystem nameSystem = World.GetOrCreateSystemManaged<NameSystem>();

                for (int i = 0; i < entities.Length; i++)
                {
                    Entity area = entities[i];
                    Entity prefabEntity = em.GetComponentData<PrefabRef>(area).m_Prefab;

                    int labelVertices = em.HasBuffer<LabelVertex>(area)
                        ? em.GetBuffer<LabelVertex>(area, true).Length
                        : -1;
                    int labelExtents = em.HasBuffer<LabelExtents>(area)
                        ? em.GetBuffer<LabelExtents>(area, true).Length
                        : -1;
                    string debugName = nameSystem.GetRenderedLabelName(area);

                    Mod.log.Info($"[Communes][Nom] area={area} prefab={prefabEntity} " +
                                 $"hasGeometry={em.HasComponent<Game.Areas.Geometry>(area)} " +
                                 $"LabelVertex.length={labelVertices} LabelExtents.length={labelExtents} " +
                                 $"GetRenderedLabelName=\"{debugName}\"");
                }
            }
            finally
            {
                entities.Dispose();
            }

            return true;
        }
    }
}

