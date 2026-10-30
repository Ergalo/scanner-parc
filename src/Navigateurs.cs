// Les navigateurs du PC et leurs profils : tous ceux de la famille Chromium (Chrome, Edge, Brave, Opera, Vivaldi, Arc…) et
// de la famille Firefox (Firefox, Waterfox, LibreWolf, Zen, Floorp…), connus par leur dossier ou repérés par leurs fichiers.
// De l'historique, on ne garde que le nom du site et un nombre de visites : l'adresse complète n'est jamais conservée.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace Ergalo.Releve
{
    internal enum Moteur { Chromium, Firefox }

    internal sealed class Profil
    {
        public string Navigateur; // identifiant court : chrome, edge, firefox…
        public Moteur Moteur;
        public string Dossier;
    }

    internal static class Navigateurs
    {
        // Dossiers « User Data » connus, relatifs à %LOCALAPPDATA% (L) ou %APPDATA% (R).
        private static readonly string[][] ChromiumConnus =
        {
            new[] { "chrome", "L", @"Google\Chrome\User Data" },
            new[] { "chrome", "L", @"Google\Chrome Beta\User Data" },
            new[] { "chrome", "L", @"Google\Chrome Dev\User Data" },
            new[] { "chrome", "L", @"Google\Chrome SxS\User Data" },
            new[] { "edge", "L", @"Microsoft\Edge\User Data" },
            new[] { "edge", "L", @"Microsoft\Edge Beta\User Data" },
            new[] { "edge", "L", @"Microsoft\Edge Dev\User Data" },
            new[] { "edge", "L", @"Microsoft\Edge SxS\User Data" },
            new[] { "brave", "L", @"BraveSoftware\Brave-Browser\User Data" },
            new[] { "vivaldi", "L", @"Vivaldi\User Data" },
            new[] { "chromium", "L", @"Chromium\User Data" },
            new[] { "yandex", "L", @"Yandex\YandexBrowser\User Data" },
            new[] { "opera", "R", @"Opera Software\Opera Stable" },
            new[] { "opera-gx", "R", @"Opera Software\Opera GX Stable" },
            new[] { "opera", "R", @"Opera Software\Opera Beta" },
        };

        private static readonly string[][] FirefoxConnus =
        {
            new[] { "firefox", @"Mozilla\Firefox\Profiles" },
            new[] { "waterfox", @"Waterfox\Profiles" },
            new[] { "librewolf", @"librewolf\Profiles" },
            new[] { "zen", @"zen\Profiles" },
            new[] { "floorp", @"Floorp\Profiles" },
            new[] { "seamonkey", @"Mozilla\SeaMonkey\Profiles" },
        };

        /// <summary>Tous les profils de tous les navigateurs trouvés, sans doublon.</summary>
        public static List<Profil> Trouver(Environnement env)
        {
            var profils = new List<Profil>();
            var vus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var c in ChromiumConnus)
            {
                string racine = Path.Combine(c[1] == "L" ? env.LocalAppData : env.AppData, c[2]);
                AjouterChromium(profils, vus, c[0], racine);
            }
            // Arc est une application du Store : son dossier porte un suffixe propre à l'installation.
            foreach (string paquet in SousDossiers(Path.Combine(env.LocalAppData, "Packages")))
                if (Path.GetFileName(paquet).StartsWith("TheBrowserCompany.Arc", StringComparison.OrdinalIgnoreCase))
                    AjouterChromium(profils, vus, "arc", Path.Combine(paquet, @"LocalCache\Local\Arc\User Data"));

            foreach (var f in FirefoxConnus)
                AjouterFirefox(profils, vus, f[0], Path.Combine(env.AppData, f[1]));

            // Les autres : tout dossier qui a la forme d'un navigateur Chromium (« Local State » à côté des profils)
            // ou Firefox (« profiles.ini »), à trois niveaux au plus sous AppData.
            // Les fenêtres web intégrées aux applications (Copilot, nouvel Outlook, OneDrive…) et les applications de bureau
            // construites comme un navigateur (Slack, Figma…) ont la même forme : on les écarte, ce ne sont pas des navigateurs.
            foreach (string racine in new[] { env.LocalAppData, env.AppData })
            {
                foreach (string dossier in Parcourir(racine, 3))
                {
                    if (File.Exists(Path.Combine(dossier, "Local State")) && !Integre(racine, dossier))
                        AjouterChromium(profils, vus, Identifiant(racine, dossier), dossier, false);
                    if (File.Exists(Path.Combine(dossier, "profiles.ini")) && !dossier.ToLowerInvariant().Contains("thunderbird"))
                        AjouterFirefox(profils, vus, Identifiant(racine, dossier), Path.Combine(dossier, "Profiles"));
                }
            }
            return profils;
        }

        private static void AjouterChromium(List<Profil> profils, HashSet<string> vus, string id, string racine, bool connu = true)
        {
            if (!Directory.Exists(racine)) return;
            // Opera garde l'historique à la racine ; les autres, un dossier par profil (Default, Profile 1…). Pour un
            // navigateur repéré par ses fichiers, on exige des dossiers de profil : une application de bureau n'en a pas.
            var candidats = new List<string>();
            if (connu)
            {
                candidats.Add(racine);
                candidats.AddRange(SousDossiers(racine));
                candidats.AddRange(SousDossiers(Path.Combine(racine, "_side_profiles")));
            }
            else
            {
                foreach (string d in SousDossiers(racine))
                {
                    string nom = Path.GetFileName(d);
                    if (nom == "Default" || nom.StartsWith("Profile ", StringComparison.Ordinal)) candidats.Add(d);
                }
            }
            foreach (string d in candidats)
            {
                if (!File.Exists(Path.Combine(d, "History"))) continue;
                if (vus.Add(Path.GetFullPath(d))) profils.Add(new Profil { Navigateur = id, Moteur = Moteur.Chromium, Dossier = d });
            }
        }

        private static void AjouterFirefox(List<Profil> profils, HashSet<string> vus, string id, string dossierProfils)
        {
            foreach (string d in SousDossiers(dossierProfils))
            {
                if (!File.Exists(Path.Combine(d, "places.sqlite"))) continue;
                if (vus.Add(Path.GetFullPath(d))) profils.Add(new Profil { Navigateur = id, Moteur = Moteur.Firefox, Dossier = d });
            }
        }

        /// <summary>Dossier d'une fenêtre web intégrée à une application (WebView2, Copilot…), pas d'un navigateur.</summary>
        private static bool Integre(string racine, string dossier)
        {
            string relatif = dossier.Substring(racine.Length).Trim('\\', '/').ToLowerInvariant();
            return relatif.Contains("webview") || relatif.StartsWith(@"microsoft\");
        }

        /// <summary>Identifiant court tiré du nom de l'éditeur : « Thorium\User Data » donne « thorium ».</summary>
        private static string Identifiant(string racine, string dossier)
        {
            string relatif = dossier.Substring(racine.Length).Trim('\\', '/');
            string premier = relatif.Split('\\', '/')[0].ToLowerInvariant();
            var propre = new string(premier.Where(ch => (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '-').ToArray());
            return propre.Length > 0 ? propre : "autre";
        }

        private static IEnumerable<string> SousDossiers(string dossier)
        {
            try { return Directory.Exists(dossier) ? Directory.GetDirectories(dossier) : new string[0]; }
            catch (Exception) { return new string[0]; }
        }

        private static IEnumerable<string> Parcourir(string racine, int profondeur)
        {
            if (profondeur == 0 || string.IsNullOrEmpty(racine)) yield break;
            foreach (string d in SousDossiers(racine))
            {
                string nom = Path.GetFileName(d);
                // Le dossier des paquets du Store est immense et ne contient pas de navigateur à ce niveau.
                if (nom.Equals("Packages", StringComparison.OrdinalIgnoreCase) || nom.Equals("Temp", StringComparison.OrdinalIgnoreCase)) continue;
                yield return d;
                foreach (string s in Parcourir(d, profondeur - 1)) yield return s;
            }
        }

        // ---------- Historique ----------

        /// <summary>
        /// Visites des <paramref name="jours"/> derniers jours, par nom d'hôte. Le fichier d'historique est copié (il est
        /// verrouillé quand le navigateur est ouvert), lu, puis la copie est effacée.
        /// </summary>
        public static Dictionary<string, long> Historique(Profil profil, DateTime maintenant, int jours)
        {
            var parHote = new Dictionary<string, long>();
            DateTime depuis = maintenant.AddDays(-jours);
            string fichier, sql;
            long seuil;
            if (profil.Moteur == Moteur.Chromium)
            {
                fichier = "History";
                // Microsecondes depuis le 1er janvier 1601.
                seuil = (depuis - new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Ticks / 10;
                sql = "SELECT u.url, COUNT(*) FROM visits v JOIN urls u ON u.id = v.url WHERE v.visit_time >= ?1 GROUP BY u.url";
            }
            else
            {
                fichier = "places.sqlite";
                // Microsecondes depuis le 1er janvier 1970.
                seuil = (depuis - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Ticks / 10;
                sql = "SELECT p.url, COUNT(*) FROM moz_historyvisits v JOIN moz_places p ON p.id = v.place_id WHERE v.visit_date >= ?1 GROUP BY p.url";
            }

            string copie = Path.Combine(Path.GetTempPath(), "ergalo-releve-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(copie);
            try
            {
                string cible = Path.Combine(copie, "h.sqlite");
                Copier(Path.Combine(profil.Dossier, fichier), cible);
                foreach (string suffixe in new[] { "-wal", "-journal" })
                {
                    string annexe = Path.Combine(profil.Dossier, fichier + suffixe);
                    if (File.Exists(annexe)) Copier(annexe, cible + suffixe);
                }
                Sqlite.Lire(cible, sql, new[] { seuil }, (adresse, visites) =>
                {
                    string hote = Domaines.NomHote(adresse);
                    if (hote == null) return;
                    long deja;
                    parHote.TryGetValue(hote, out deja);
                    parHote[hote] = deja + visites;
                });
            }
            finally
            {
                try { Directory.Delete(copie, true); } catch (Exception) { }
            }
            return parHote;
        }

        private static void Copier(string source, string cible)
        {
            // Partage complet : le navigateur peut rester ouvert pendant la copie.
            using (var entree = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var sortie = new FileStream(cible, FileMode.CreateNew, FileAccess.Write))
                entree.CopyTo(sortie);
        }

        // ---------- Extensions ----------

        /// <summary>Noms des extensions installées dans ce profil (ni les thèmes, ni les modules internes).</summary>
        public static List<string> Extensions(Profil profil)
        {
            var noms = new List<string>();
            try
            {
                if (profil.Moteur == Moteur.Chromium) ExtensionsChromium(profil.Dossier, noms);
                else ExtensionsFirefox(profil.Dossier, noms);
            }
            catch (Exception) { }
            return noms.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct().ToList();
        }

        private static void ExtensionsChromium(string profil, List<string> noms)
        {
            var json = new JavaScriptSerializer();
            foreach (string extension in SousDossiers(Path.Combine(profil, "Extensions")))
            {
                string version = SousDossiers(extension).OrderByDescending(d => d).FirstOrDefault();
                if (version == null) continue;
                string manifeste = Path.Combine(version, "manifest.json");
                if (!File.Exists(manifeste)) continue;
                try
                {
                    var m = json.DeserializeObject(File.ReadAllText(manifeste)) as Dictionary<string, object>;
                    if (m == null || m.ContainsKey("theme")) continue;
                    string nom = m.ContainsKey("name") ? m["name"] as string : null;
                    if (nom != null && nom.StartsWith("__MSG_"))
                        nom = Traduire(json, version, m.ContainsKey("default_locale") ? m["default_locale"] as string : null, nom);
                    if (nom != null && !nom.StartsWith("__MSG_")) noms.Add(nom);
                }
                catch (Exception) { }
            }
        }

        private static string Traduire(JavaScriptSerializer json, string dossier, string langue, string cle)
        {
            string nomCle = cle.Substring(6).TrimEnd('_');
            foreach (string l in new[] { "fr", langue, "en", "en_US" })
            {
                if (l == null) continue;
                string fichier = Path.Combine(dossier, "_locales", l, "messages.json");
                if (!File.Exists(fichier)) continue;
                var messages = json.DeserializeObject(File.ReadAllText(fichier)) as Dictionary<string, object>;
                if (messages == null) continue;
                foreach (var kv in messages)
                {
                    if (!kv.Key.Equals(nomCle, StringComparison.OrdinalIgnoreCase)) continue;
                    var entree = kv.Value as Dictionary<string, object>;
                    if (entree != null && entree.ContainsKey("message")) return entree["message"] as string;
                }
            }
            return null;
        }

        private static void ExtensionsFirefox(string profil, List<string> noms)
        {
            string fichier = Path.Combine(profil, "extensions.json");
            if (!File.Exists(fichier)) return;
            var racine = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 }.DeserializeObject(File.ReadAllText(fichier)) as Dictionary<string, object>;
            if (racine == null || !racine.ContainsKey("addons")) return;
            var modules = racine["addons"] as object[];
            if (modules == null) return;
            foreach (object o in modules)
            {
                var a = o as Dictionary<string, object>;
                if (a == null) continue;
                if (!"extension".Equals(a.ContainsKey("type") ? a["type"] : null)) continue;
                if (!"app-profile".Equals(a.ContainsKey("location") ? a["location"] : null)) continue;
                var local = a.ContainsKey("defaultLocale") ? a["defaultLocale"] as Dictionary<string, object> : null;
                if (local != null && local.ContainsKey("name")) noms.Add(local["name"] as string);
            }
        }
    }
}
