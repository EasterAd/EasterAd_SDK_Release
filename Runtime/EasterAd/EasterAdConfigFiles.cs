using System.IO;
using UnityEngine;

namespace EasterAd
{
    /// <summary>
    /// <para xml:lang="ko">EasterAd 런타임 설정 파일 경로를 확인하는 헬퍼입니다.</para>
    /// <para xml:lang="en">Helper for resolving EasterAd runtime configuration file paths.</para>
    /// </summary>
    public static class EasterAdConfigFiles
    {
        /// <summary>
        /// <para xml:lang="ko">현재 EasterAd 설정 파일 이름입니다.</para>
        /// <para xml:lang="en">The current EasterAd configuration file name.</para>
        /// </summary>
        public const string ConfigFileName = "EasterAd_Config.txt";

        /// <summary>
        /// <para xml:lang="ko">이전 SDK 버전에서 사용하던 설정 파일 이름입니다.</para>
        /// <para xml:lang="en">The configuration file name used by earlier SDK versions.</para>
        /// </summary>
        public const string LegacyConfigFileName = "ETA_Config.txt";

        /// <summary>
        /// <para xml:lang="ko">현재 EasterAd 입력 축 캐시 파일 이름입니다.</para>
        /// <para xml:lang="en">The current EasterAd input-axis cache file name.</para>
        /// </summary>
        public const string AxesFileName = "EasterAd_Axes.txt";

        /// <summary>
        /// <para xml:lang="ko">이전 SDK 버전에서 사용하던 입력 축 캐시 파일 이름입니다.</para>
        /// <para xml:lang="en">The input-axis cache file name used by earlier SDK versions.</para>
        /// </summary>
        public const string LegacyAxesFileName = "ETA_Axes.txt";

        /// <summary>
        /// <para xml:lang="ko">현재 설정 파일 경로를 반환합니다.</para>
        /// <para xml:lang="en">Returns the current configuration file path.</para>
        /// </summary>
        public static string CurrentConfigPath => Path.Combine(Application.streamingAssetsPath, ConfigFileName);

        /// <summary>
        /// <para xml:lang="ko">이전 설정 파일 경로를 반환합니다.</para>
        /// <para xml:lang="en">Returns the legacy configuration file path.</para>
        /// </summary>
        public static string LegacyConfigPath => Path.Combine(Application.streamingAssetsPath, LegacyConfigFileName);

        /// <summary>
        /// <para xml:lang="ko">현재 입력 축 캐시 파일 경로를 반환합니다.</para>
        /// <para xml:lang="en">Returns the current input-axis cache file path.</para>
        /// </summary>
        public static string CurrentAxesPath => Path.Combine(Application.streamingAssetsPath, AxesFileName);

        /// <summary>
        /// <para xml:lang="ko">이전 입력 축 캐시 파일 경로를 반환합니다.</para>
        /// <para xml:lang="en">Returns the legacy input-axis cache file path.</para>
        /// </summary>
        public static string LegacyAxesPath => Path.Combine(Application.streamingAssetsPath, LegacyAxesFileName);

        /// <summary>
        /// <para xml:lang="ko">존재하는 설정 파일 경로를 반환합니다. 현재 파일이 없으면 이전 파일을 fallback으로 사용합니다.</para>
        /// <para xml:lang="en">Returns an existing configuration path. Falls back to the legacy file when the current file is absent.</para>
        /// </summary>
        public static string ResolveConfigPath()
        {
            if (File.Exists(CurrentConfigPath)) return CurrentConfigPath;
            if (File.Exists(LegacyConfigPath)) return LegacyConfigPath;
            return CurrentConfigPath;
        }

        /// <summary>
        /// <para xml:lang="ko">존재하는 입력 축 캐시 파일 경로를 반환합니다. 현재 파일이 없으면 이전 파일을 fallback으로 사용합니다.</para>
        /// <para xml:lang="en">Returns an existing input-axis cache path. Falls back to the legacy file when the current file is absent.</para>
        /// </summary>
        public static string ResolveAxesPath()
        {
            if (File.Exists(CurrentAxesPath)) return CurrentAxesPath;
            if (File.Exists(LegacyAxesPath)) return LegacyAxesPath;
            return CurrentAxesPath;
        }

        /// <summary>
        /// <para xml:lang="ko">설정 파일을 읽습니다. 현재 파일이 없으면 이전 파일을 읽습니다.</para>
        /// <para xml:lang="en">Reads the configuration file. Reads the legacy file when the current file is absent.</para>
        /// </summary>
        public static bool TryReadConfig(out string[] config, out string path)
        {
            path = ResolveConfigPath();
            if (!File.Exists(path))
            {
                config = new string[0];
                return false;
            }

            config = File.ReadAllLines(path);
            return true;
        }
    }
}
