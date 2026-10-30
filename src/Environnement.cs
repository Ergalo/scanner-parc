// Ce que le relevé lit sur le PC : les dossiers des navigateurs, la liste des applications installées et les lancements au
// démarrage. Deux sources : le vrai PC (Windows) et un dossier d'essai fictif (--essai), pour les tests et les démonstrations,
// qui ne touche ni au registre ni aux dossiers de la personne.
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace Ergalo.Releve
{
    internal sealed class ProgrammeInstalle
    {
        public string Nom;
        public string Executable; // nom du fichier seul (notion.exe), jamais le chemin
    }

    internal sealed class Lancement
    {
        public string Nom;
        public string Executable;
    }

    internal abstract class Environnement
    {
        public string AppData;       // %APPDATA% (Roaming)
        public string LocalAppData;  // %LOCALAPPDATA%
        public string[] DossiersDemarrage;
        public DateTime Maintenant;

        public abstract IEnumerable<ProgrammeInstalle> ProgrammesInstalles();
        public abstract IEnumerable<Lancement> LancementsRegistre();
        public abstract IEnumerable<string> PaquetsStore();

        /// <summary>Nom du fichier .exe d'une commande ou d'une icône, sans dossier ni argument ; null sinon.</summary>
        public static string Executable(string commande)
        {
            if (string.IsNullOrWhiteSpace(commande)) return null;
            string c = commande.Trim();
            string chemin;
            if (c.StartsWith("\""))
            {
                int fin = c.IndexOf('"', 1);
                chemin = fin > 0 ? c.Substring(1, fin - 1) : c.Substring(1);
            }
            else
            {
                int exe = c.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                chemin = exe >= 0 ? c.Substring(0, exe + 4) : c.Split(' ', ',')[0];
            }
            int virgule = chemin.LastIndexOf(',');
            if (virgule > 0 && !chemin.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) chemin = chemin.Substring(0, virgule);
            string nom;
            try { nom = Path.GetFileName(chemin.Trim()); }
            catch (ArgumentException) { return null; }
            if (nom == null || !nom.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return null;
            return nom.ToLowerInvariant();
        }
    }

    internal sealed class EnvironnementWindows : Environnement
    {
        private const string Desinstallation = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        private const string Run = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string Paquets = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

        public EnvironnementWindows()
        {
            AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            DossiersDemarrage = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            };
            Maintenant = DateTime.UtcNow;
        }

        private static IEnumerable<RegistryKey> Racines(string chemin)
        {
            var vues = new[]
            {
                new KeyValuePair<RegistryHive, RegistryView>(RegistryHive.LocalMachine, RegistryView.Registry64),
                new KeyValuePair<RegistryHive, RegistryView>(RegistryHive.LocalMachine, RegistryView.Registry32),
                new KeyValuePair<RegistryHive, RegistryView>(RegistryHive.CurrentUser, RegistryView.Registry64),
                new KeyValuePair<RegistryHive, RegistryView>(RegistryHive.CurrentUser, RegistryView.Registry32),
            };
            foreach (var v in vues)
            {
                RegistryKey cle = null;
                try { cle = RegistryKey.OpenBaseKey(v.Key, v.Value).OpenSubKey(chemin); }
                catch (Exception) { }
                if (cle != null) yield return cle;
            }
        }

        public override IEnumerable<ProgrammeInstalle> ProgrammesInstalles()
        {
            var liste = new List<ProgrammeInstalle>();
            foreach (RegistryKey racine in Racines(Desinstallation))
            {
                using (racine)
                {
                    foreach (string sous in racine.GetSubKeyNames())
                    {
                        try
                        {
                            using (RegistryKey cle = racine.OpenSubKey(sous))
                            {
                                if (cle == null) continue;
                                string nom = cle.GetValue("DisplayName") as string;
                                if (string.IsNullOrWhiteSpace(nom)) continue;
                                object systeme = cle.GetValue("SystemComponent");
                                if (systeme is int && (int)systeme == 1) continue;
                                if (cle.GetValue("ParentKeyName") != null) continue;
                                string type = cle.GetValue("ReleaseType") as string;
                                if (type != null && (type.Contains("Update") || type.Contains("Hotfix"))) continue;
                                liste.Add(new ProgrammeInstalle { Nom = nom.Trim(), Executable = Executable(cle.GetValue("DisplayIcon") as string) });
                            }
                        }
                        catch (Exception) { }
                    }
                }
            }
            return liste;
        }

        public override IEnumerable<Lancement> LancementsRegistre()
        {
            var liste = new List<Lancement>();
            foreach (RegistryKey racine in Racines(Run))
            {
                using (racine)
                {
                    foreach (string nom in racine.GetValueNames())
                    {
                        if (string.IsNullOrWhiteSpace(nom)) continue;
                        liste.Add(new Lancement { Nom = nom.Trim(), Executable = Executable(racine.GetValue(nom) as string) });
                    }
                }
            }
            return liste;
        }

        public override IEnumerable<string> PaquetsStore()
        {
            var liste = new List<string>();
            try
            {
                using (RegistryKey cle = Registry.CurrentUser.OpenSubKey(Paquets))
                {
                    if (cle != null) liste.AddRange(cle.GetSubKeyNames());
                }
            }
            catch (Exception) { }
            return liste;
        }
    }

    /// <summary>
    /// PC fictif : un dossier qui contient AppData\Roaming, AppData\Local, Demarrage\ et registre.tsv
    /// (lignes « installe », « demarrage » ou « paquet », champs séparés par des tabulations).
    /// </summary>
    internal sealed class EnvironnementEssai : Environnement
    {
        private readonly List<string[]> registre = new List<string[]>();

        public EnvironnementEssai(string racine, DateTime maintenant)
        {
            AppData = Path.Combine(racine, @"AppData\Roaming");
            LocalAppData = Path.Combine(racine, @"AppData\Local");
            DossiersDemarrage = new[] { Path.Combine(racine, "Demarrage") };
            Maintenant = maintenant;
            string fichier = Path.Combine(racine, "registre.tsv");
            if (File.Exists(fichier))
                foreach (string ligne in File.ReadAllLines(fichier))
                    if (ligne.Length > 0 && ligne[0] != '#') registre.Add(ligne.Split('\t'));
        }

        private static string Champ(string[] l, int i) { return l.Length > i ? l[i] : null; }

        public override IEnumerable<ProgrammeInstalle> ProgrammesInstalles()
        {
            foreach (var l in registre)
                if (l[0] == "installe") yield return new ProgrammeInstalle { Nom = l[1], Executable = Executable(Champ(l, 2)) };
        }

        public override IEnumerable<Lancement> LancementsRegistre()
        {
            foreach (var l in registre)
                if (l[0] == "demarrage") yield return new Lancement { Nom = l[1], Executable = Executable(Champ(l, 2)) };
        }

        public override IEnumerable<string> PaquetsStore()
        {
            foreach (var l in registre)
                if (l[0] == "paquet") yield return l[1];
        }
    }
}
