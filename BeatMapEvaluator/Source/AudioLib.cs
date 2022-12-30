using System.Runtime.InteropServices;

namespace BeatMapEvaluator
{
    /// <summary>A C# Binding class for handling calls to "AudioLib.dll"</summary>
    public partial class AudioLib {
        /// <summary>
        /// Gets the length of a given audio file in seconds 
        /// </summary>
        /// <remarks>
        /// <c>Note: This function is a C# binding to the C based "AudioLib.dll"</c>
        /// </remarks>
        /// <param name="FilePath">The audio files path</param>
        /// <param name="error">A return int for error callbacks</param>
        /// <returns>Length in seconds</returns>
        [DllImport("AudioLib.dll", CallingConvention=CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern float GetAudioTime([MarshalAs(UnmanagedType.LPStr)] string FilePath, ref int error);

        /// <summary>
        /// Gets the length of a given audio file in seconds
        /// </summary>
        /// <param name="filePath">The audio files path</param>
        /// <returns>Length in seconds</returns>
        public static float GetAudioLength(string filePath) {
            int errorCode = 0;
            float value = GetAudioTime(filePath, ref errorCode);
            if(errorCode != 0)
                UserConsole.LogError($"Audio error: CODE-{errorCode}");
            return value;
        }
    }
}
