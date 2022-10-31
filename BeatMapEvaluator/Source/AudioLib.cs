using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BeatMapEvaluator
{
    public partial class AudioLib {
        [DllImport("AudioLib.dll", CallingConvention=CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern float GetAudioTime([MarshalAs(UnmanagedType.LPStr)] string FilePath, ref int error);

        public static float GetAudioLength(string filePath) {
            int errorCode = 0;
            float value = GetAudioTime(filePath, ref errorCode);
            if(errorCode != 0)
                UserConsole.LogError($"Audio error: CODE-{errorCode}");
            return value;
        }
    }
}
