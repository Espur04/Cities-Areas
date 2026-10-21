using System.Reflection;
using Game;
using Unity.Entities;

namespace Communes
{
    internal static class CommuneSystemDependency
    {
        private static readonly MethodInfo kCompleteDependencyMethod = typeof(SystemBase).GetMethod(
            "CompleteDependency", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void Complete(GameSystemBase system)
        {
            kCompleteDependencyMethod?.Invoke(system, null);
        }
    }
}
