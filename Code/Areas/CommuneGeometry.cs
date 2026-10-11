using Colossal.Mathematics;
using Game.Areas;
using Game.Objects;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Communes
{

public static class CommuneGeometry
    {

public static bool Contains(
            in Bounds3 bounds,
            DynamicBuffer<Node> nodes,
            DynamicBuffer<Triangle> triangles,
            float3 point)
        {

if (point.x < bounds.min.x || point.x > bounds.max.x
                || point.z < bounds.min.z || point.z > bounds.max.z)
            {
                return false;
            }

            float2 p = point.xz;
            for (int i = 0; i < triangles.Length; i++)
            {

                Triangle3 tri3 = AreaUtils.GetTriangle3(nodes, triangles[i]);
                if (MathUtils.Intersect(tri3.xz, p, out _))
                {
                    return true;
                }
            }

            return false;
        }

public static bool ContainsTransform(
            in Bounds3 bounds,
            DynamicBuffer<Node> nodes,
            DynamicBuffer<Triangle> triangles,
            in Transform transform)
        {
            return Contains(bounds, nodes, triangles, transform.m_Position);
        }
    }
}
