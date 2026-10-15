using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

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
