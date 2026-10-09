using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Garden.Tests
{
    // Quy tắc của dự án mà trình biên dịch không kiểm tra giúp
    public class ProjectRulesTests
    {
        // Unity chỉ lưu được một component vào scene khi class nằm trong file trùng tên class.
        // Sai quy tắc này thì mọi thứ vẫn chạy trong Editor (thêm component bằng code), nhưng scene lưu ra báo
        // "missing script" và tính năng biến mất trong bản build.
        [Test]
        public void EveryComponentClass_LivesInAFileWithTheSameName()
        {
            var classesWithScripts = new HashSet<Type>();
            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/script" }))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                Type type = script != null ? script.GetClass() : null;
                if (type != null) classesWithScripts.Add(type);
            }

            var misplaced = new List<string>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                bool ours = (name.StartsWith("Garden.") && name != "Garden.Tests") || name == "Assembly-CSharp";
                if (!ours) continue;

                foreach (Type type in assembly.GetTypes())
                {
                    bool isComponent = typeof(MonoBehaviour).IsAssignableFrom(type) && !type.IsAbstract && !type.IsGenericType;
                    if (isComponent && !classesWithScripts.Contains(type)) misplaced.Add(type.FullName);
                }
            }

            Assert.IsEmpty(misplaced, "các component này phải nằm trong file riêng trùng tên class: " + string.Join(", ", misplaced));
        }
    }
}
