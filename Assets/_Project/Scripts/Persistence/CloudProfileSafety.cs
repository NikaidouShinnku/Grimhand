using System;
using System.Globalization;
using UnityEngine;

namespace Grimhand.Persistence
{
    /// <summary>Validate before touching local/cloud files; never migrate an unverified payload.</summary>
    public static class CloudProfileSafety
    {
        public static bool TryValidate(string json,SaveValidationContext context,out DateTime utc,out string error)
        {
            utc=DateTime.MinValue;error="";
            try
            {
                if(string.IsNullOrWhiteSpace(json)){error="存档为空。";return false;}
                var dto=JsonUtility.FromJson<PlayerProfileSaveData>(json);
                if(dto==null||dto.saveVersion<1||dto.saveVersion>PlayerProfileState.CurrentSaveVersion)
                {error="存档版本不兼容，已保留原文件。";return false;}
                if(!SaveIntegrity.Verify(dto)){error="存档签名无效，已保留原文件。";return false;}
                if(context!=null&&!SaveValidator.TryValidate(dto,context,out error))return false;
                if(!DateTime.TryParse(dto.lastSavedUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var time))
                {error="存档保存时间无效。";return false;}
                utc=time.ToUniversalTime();return true;
            }
            catch(Exception ex){error="存档无法解析："+ex.Message;return false;}
        }

        public static bool CanUpload(string local,string remote,SaveValidationContext context,out string error)
        {
            if(!TryValidate(local,context,out var localTime,out error))return false;
            if(remote==null)return true;
            if(!TryValidate(remote,context,out var remoteTime,out error))return false;
            if(remoteTime>localTime){error="云端存档较新，已阻止覆盖；本地进度仍保留。";return false;}
            return true;
        }
    }
}
