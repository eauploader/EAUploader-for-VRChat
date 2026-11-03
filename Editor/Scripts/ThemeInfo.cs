using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

// Unity 2022.3 uses C# 9; file-scoped namespaces require C# 10. skipcq: CS-R1062
namespace EAUploader.UI
{
    public class ThemeInfo
    {
        public string name;
        public string display;

        public ThemeInfo(string name, string display)
        {
            this.name = name;
            this.display = display;
        }
    }
}
