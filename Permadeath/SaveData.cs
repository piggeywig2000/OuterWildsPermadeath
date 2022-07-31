using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Permadeath
{
    internal static class SaveManager
    {
        private const string FILENAME = "permadeathsave.json";

        public static SaveData Data { get; private set; }

        static SaveManager()
        {
            Load();
        }

        public static void Save()
        {
            Permadeath.SharedModHelper.Storage.Save(Data, FILENAME);
        }

        public static void Load()
        {
            Data = Permadeath.SharedModHelper.Storage.Load<SaveData>(FILENAME);
            if (Data == null) Data = new SaveData();
        }
    }

    internal class SaveData
    {
        public List<SaveProfile> Profiles { get; set; } = new List<SaveProfile>();

        public SaveProfile GetOrAddProfile(string name)
        {
            SaveProfile profile = Profiles.FirstOrDefault(p => p.Name == name);
            if (profile == null)
            {
                profile = new SaveProfile() { Name = name };
                Profiles.Add(profile);
            }

            return profile;
        }

        public SaveProfile GetOrAddCurrentProfile() => GetOrAddProfile(StandaloneProfileManager.SharedInstance.currentProfile.profileName);

        public void RemoveProfile(string name)
        {
            Profiles.Remove(GetOrAddProfile(name));
        }
    }

    internal class SaveProfile
    {
        public string Name { get; set; }
        public bool PermadeathEnabled { get; set; } = false;
        public bool SafeQuit { get; set; } = true;
    }
}
