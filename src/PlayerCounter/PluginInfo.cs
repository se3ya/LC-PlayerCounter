using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace PlayerCounter
{

    public static class MyPluginInfo
    {
        public const string PLUGIN_GUID = "com.seeya.mblcp.playercounter";
        public const string PLUGIN_NAME = "Player Tracker";
        public const string PLUGIN_VERSION = "1.0.0";
    }
}
