using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using static EAUploader.Config;
using static EAUploader.T7e;

// Unity 2022.3 uses C# 9; file-scoped namespaces require C# 10. skipcq: CS-R1062
namespace EAUploader
{
    public class LanguageInfo
    {
        public string name { get; set; }
        public string display { get; set; }
    }
}
