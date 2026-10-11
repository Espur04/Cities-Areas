using System.Reflection;
using Game.Areas;
using Game.Buildings;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Communes
{

public partial class CommuneAreaToolSystem : AreaToolSystem
    {
        private static readonly FieldInfo kControlPointsField = typeof(AreaToolSystem).GetField(
            "m_ControlPoints", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo kStateField = typeof(AreaToolSystem).GetField(
            "m_State", BindingFlags.Instance | BindingFlags.NonPublic);
        private EntityQuery m_DistrictQuery;

public override string toolID => "Area Tool";

        protected override void OnCreate()
        {
            base.OnCreate();
            m_DistrictQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<District>(), ComponentType.ReadOnly<Area>(),
                    ComponentType.ReadOnly<Node>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Hidden>()
                }
            });

int ownIndex = m_ToolSystem.tools.IndexOf(this);
            int nativeIndex = m_ToolSystem.tools.FindIndex(tool =>
                tool.GetType() == typeof(AreaToolSystem));
            if (ownIndex >= 0 && nativeIndex >= 0 && ownIndex > nativeIndex)
            {
                m_ToolSystem.tools.RemoveAt(ownIndex);
                m_ToolSystem.tools.Insert(nativeIndex, this);
            }
        }

        public override bool TrySetPrefab(PrefabBase prefab)
        {
            return prefab is CommuneAreaPrefab && base.TrySetPrefab(prefab);
        }

        public override void GetAvailableSnapMask(out Snap onMask, out Snap offMask)
        {

            onMask = Snap.ExistingGeometry | Snap.StraightDirection | Snap.NetMiddle;
            offMask = onMask;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            JobHandle dependencies = base.OnUpdate(inputDeps);
            if (m_ToolSystem.activeTool == this && m_ToolSystem.activePrefab is CommuneAreaPrefab)
            {

requireAreas |= AreaUtils.GetTypeMask(AreaType.Space);
                requireAreas |= AreaUtils.GetTypeMask(AreaType.District);
            }
            return dependencies;
        }

        protected override bool GetRaycastResult(out ControlPoint controlPoint)
        {
            if (!base.GetRaycastResult(out controlPoint))
                return false;

if (controlPoint.m_OriginalEntity != Entity.Null
                && EntityManager.HasComponent<District>(controlPoint.m_OriginalEntity))
            {
                controlPoint.m_OriginalEntity = Entity.Null;
                controlPoint.m_ElementIndex = new int2(-1, -1);
            }

if (controlPoint.m_OriginalEntity != Entity.Null
                && EntityManager.HasComponent<Building>(controlPoint.m_OriginalEntity))
            {
                controlPoint.m_OriginalEntity = Entity.Null;
                controlPoint.m_ElementIndex = new int2(-1, -1);
            }

            if (!m_DistrictQuery.IsEmptyIgnoreFilter)
                SnapToDistricts(ref controlPoint, AreaUtils.GetMinNodeDistance(AreaType.District));

            return true;
        }

        private void SnapToDistricts(ref ControlPoint controlPoint, float snapDistance)
        {
            NativeArray<Entity> districts = m_DistrictQuery.ToEntityArray(Allocator.Temp);
            ControlPoint best = controlPoint;
            bool found = false;
            try
            {
                float2 hit = controlPoint.m_HitPosition.xz;
                for (int i = 0; i < districts.Length; i++)
                {
                    Entity district = districts[i];
                    DynamicBuffer<Node> nodes = EntityManager.GetBuffer<Node>(district, true);
                    if (nodes.Length < 2)
                        continue;

                    Area area = EntityManager.GetComponentData<Area>(district);
                    int edgeCount = nodes.Length - 1;
                    if ((area.m_Flags & AreaFlags.Complete) != 0)
                        edgeCount = nodes.Length;

                    for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
                    {
                        float3 start = nodes[edgeIndex].m_Position;
                        float3 end = nodes[(edgeIndex + 1) % nodes.Length].m_Position;
                        float2 edge = end.xz - start.xz;
                        float edgeLengthSq = math.lengthsq(edge);
                        if (edgeLengthSq <= 0.0001f)
                            continue;

                        float t = math.saturate(math.dot(hit - start.xz, edge) / edgeLengthSq);
                        float3 snappedPosition;
                        if (math.distance(hit, start.xz) <= snapDistance
                            && math.distance(hit, start.xz) <= math.distance(hit, end.xz))
                            snappedPosition = start;
                        else if (math.distance(hit, end.xz) <= snapDistance
                            && math.distance(hit, end.xz) < math.distance(hit, start.xz))
                            snappedPosition = end;
                        else
                            snappedPosition = math.lerp(start, end, t);

                        if (math.distance(hit, snappedPosition.xz) > snapDistance)
                            continue;

                        ControlPoint candidate = controlPoint;
                        candidate.m_OriginalEntity = Entity.Null;
                        candidate.m_ElementIndex = new int2(-1, -1);
                        candidate.m_Position = snappedPosition;
                        candidate.m_Direction = math.normalizesafe(edge);
                        candidate.m_SnapPriority = ToolUtils.CalculateSnapPriority(
                            2f, 1f, 1f, controlPoint.m_HitPosition,
                            candidate.m_Position, candidate.m_Direction);

                        if (!found || ToolUtils.CompareSnapPriority(
                                candidate.m_SnapPriority, best.m_SnapPriority))
                        {
                            best = candidate;
                            found = true;
                        }
                    }
                }
            }
            finally
            {
                districts.Dispose();
            }

            if (found && ToolUtils.CompareSnapPriority(best.m_SnapPriority, controlPoint.m_SnapPriority))
                controlPoint = best;
        }

        public bool TryGetCursorPosition(out float3 position)
        {
            if (kControlPointsField?.GetValue(this) is NativeList<ControlPoint> controlPoints
                && controlPoints.IsCreated && controlPoints.Length > 0)
            {

bool creating = kStateField?.GetValue(this)?.ToString() == "Create";
                ControlPoint controlPoint = controlPoints[creating ? controlPoints.Length - 1 : 0];
                position = controlPoint.m_Position;
                return true;
            }

            position = default;
            return false;
        }
    }
}
