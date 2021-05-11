using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace AwesomeService.Classes
{
    public class RegistryManagement
    {
        /// <summary>
        /// Used to read registry key values
        /// </summary>
        /// <param name="KeyLocation"></param>
        /// <param name="Key"></param>
        /// <param name="UserContext"></param>
        /// <returns></returns>
        public static string ReadUserRegistryKey(string KeyLocation, string Key, string UserSID)
        {
            RegistryKey registryKey = null;
            string KeyValue = string.Empty;
            try
            {               
                using (registryKey = Registry.Users.OpenSubKey(UserSID + "\\" + KeyLocation))
                {
                    if (registryKey != null)
                    {
                        var Value = registryKey.GetValue(Key, null);
                        if (Value != null)
                        {
                            KeyValue = Value.ToString();
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                string exMessage = ex.Message.ToString();
                //SharedEventLog.WriteToEventLog("ReadRegistryKey", Key + KeyValue, false, EventLogEntryType.Error);
                KeyValue = "error";

            }

            finally
            {
                if (registryKey != null)
                {
                    registryKey.Dispose();
                }
            }
            return KeyValue;
        }
        public static string ReadOfflineHive(string User, string Location, string Key)
        {
            string result = null;
            string dir = "C:\\Users\\" + User;

            if (Directory.Exists(dir))
            {
               
                Console.WriteLine("Directory is:" + dir);

                string wimHivePath = Path.Combine(dir, "ntuser.dat");
                string loadedHiveKey = RegistryInterop.Load(wimHivePath);

                RegistryKey rk = Registry.Users.OpenSubKey(loadedHiveKey);

                if (rk != null)
                {
                    RegistryKey ExistKey = rk.OpenSubKey(Location, RegistryKeyPermissionCheck.ReadWriteSubTree);

                    if (ExistKey != null)
                    {
                        result = ExistKey.GetValue(Key).ToString();
                        ExistKey.Close();
                    }

                    rk.Close();
                }

                RegistryInterop.Unload();

                
            }

            return result;

        }
    }
    // Registry class for loading NTUser.dat file and 
    // temporary hive key and then unloading temporary hive key.
    public class RegistryInterop
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public int LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TOKEN_PRIVILEGES
        {
            public LUID Luid;
            public int Attributes;
            public int PrivilegeCount;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        public static extern int OpenProcessToken(int ProcessHandle, int DesiredAccess, ref int tokenhandle);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern int GetCurrentProcess();

        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        public static extern int LookupPrivilegeValue(string lpsystemname, string lpname, [MarshalAs(UnmanagedType.Struct)] ref LUID lpLuid);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        public static extern int AdjustTokenPrivileges(int tokenhandle, int disableprivs, [MarshalAs(UnmanagedType.Struct)] ref TOKEN_PRIVILEGES Newstate, int bufferlength, int PreivousState, int Returnlength);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int RegLoadKey(uint hKey, string lpSubKey, string lpFile);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int RegUnLoadKey(uint hKey, string lpSubKey);

        public const int TOKEN_ADJUST_PRIVILEGES = 0x00000020;
        public const int TOKEN_QUERY = 0x00000008;
        public const int SE_PRIVILEGE_ENABLED = 0x00000002;
        public const string SE_RESTORE_NAME = "SeRestorePrivilege";
        public const string SE_BACKUP_NAME = "SeBackupPrivilege";
        public const uint HKEY_USERS = 0x80000003;

        //temporary hive key
        public const string HIVE_SUBKEY = "Test";


        static private Boolean gotPrivileges = false;

        static private void GetPrivileges()
        {
            int token = 0;
            int retval = 0;
            TOKEN_PRIVILEGES tpRestore = new TOKEN_PRIVILEGES();
            TOKEN_PRIVILEGES tpBackup = new TOKEN_PRIVILEGES();
            LUID RestoreLuid = new LUID();
            LUID BackupLuid = new LUID();

            retval = LookupPrivilegeValue(null, SE_RESTORE_NAME, ref RestoreLuid);
            tpRestore.PrivilegeCount = 1;
            tpRestore.Attributes = SE_PRIVILEGE_ENABLED;
            tpRestore.Luid = RestoreLuid;

            retval = LookupPrivilegeValue(null, SE_BACKUP_NAME, ref BackupLuid);
            tpBackup.PrivilegeCount = 1;
            tpBackup.Attributes = SE_PRIVILEGE_ENABLED;
            tpBackup.Luid = BackupLuid;

            retval = OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, ref token);
            retval = AdjustTokenPrivileges(token, 0, ref tpRestore, 1024, 0, 0);
            retval = AdjustTokenPrivileges(token, 0, ref tpBackup, 1024, 0, 0);

            gotPrivileges = true;
        }

        static public string Load(string file)
        {
            if (!gotPrivileges)
                GetPrivileges();
            RegLoadKey(HKEY_USERS, HIVE_SUBKEY, file);
            return HIVE_SUBKEY;
        }

        static public void Unload()
        {
            if (!gotPrivileges)
                GetPrivileges();
            int output = RegUnLoadKey(HKEY_USERS, HIVE_SUBKEY);
        }
    }
    
}
