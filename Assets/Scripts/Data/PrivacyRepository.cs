using UnityEngine;

namespace SmallGames
{
    /// <summary>
    /// 记录用户是否已同意当前版本隐私政策与用户协议。与设置分开存储，重置设置不会撤销同意。
    /// </summary>
    public static class PrivacyRepository
    {
        private const string VersionKey = "SmallGames.PrivacyAgreeVersion";

        public static bool HasAgreed => SecurePrefs.GetInt(VersionKey, 0) >= ComplianceCatalog.PrivacyVersion;

        public static void Agree()
        {
            SecurePrefs.SetInt(VersionKey, ComplianceCatalog.PrivacyVersion);
            SecurePrefs.Save();
        }
    }
}
