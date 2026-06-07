using System;
using System.Collections.Generic;

namespace NexusFramework.GAS.ECS
{
    public static class TypeUtil
    {
        public static string[] GetInheritanceChain(this Type type, bool fullName = true)
        {
            var chain = new List<string>();
            var current = type;
            while (current != null)
            {
                chain.Add(fullName ? current.FullName : current.Name);
                current = current.BaseType;
            }

            return chain.ToArray();
        }
    }
}
