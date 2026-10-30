// Tests du scanner, sans bibliothèque : chaque test est une méthode, une erreur l'arrête. Tout se passe sur un PC fictif
// fabriqué dans un dossier temporaire (historiques Chromium et Firefox, extensions, registre simulé), jamais sur les
// navigateurs de la personne qui lance les tests.
//   tests.exe <chemin de ergalo-releve.exe>
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

namespace Ergalo.Releve.Tests
{
    internal static class Lanceur
    {
        private static int echecs;
        private static string exe;
        private static readonly DateTime Maintenant = new DateTime(2026, 10, 29, 14, 0, 0, DateTimeKind.Utc);

        private static int Main(string[] args)
        {
            // tests.exe --fabriquer <dossier> : laisse un PC fictif dans ce dossier, pour les démonstrations et les captures.
            if (args.Length == 2 && args[0] == "--fabriquer")
            {
                new PcFictif(args[1]);
                Console.WriteLine("PC fictif : " + args[1]);
                return 0;
            }
            exe = args.LastOrDefault(a => a.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            var tests = new List<KeyValuePair<string, Action>>
            {
                T("le nom d'hôte ne garde que le site", NomHote),
                T("un site inconnu est réduit à son domaine principal", Principal),
                T("reconnaissance des domaines comme le catalogue du site", ReconnaissanceDomaines),
                T("reconnaissance des programmes comme le catalogue du site", ReconnaissanceProgrammes),
                T("nom de fichier .exe tiré d'une commande, sans chemin", Executable),
                T("tous les navigateurs et tous les profils du PC fictif sont trouvés", NavigateursTrouves),
                T("relevé complet du PC fictif", ReleveComplet),
                T("historique ouvert par le navigateur : lu quand même", HistoriqueOuvert),
                T("historique verrouillé : signalé, le reste du relevé continue", HistoriqueVerrouille),
                T("fichier exporté : format convenu, seulement les lignes cochées, rien de personnel", ExportFichier),
                T("marque : lue dans le nom du fichier, refusée si absente, tronquée ou de date impossible", MarqueLue),
                T("programme : refuse sans marque, refuse renommé, refuse expiré, produit le fichier avec la marque", ProgrammeComplet),
            };
            foreach (var t in tests)
            {
                try
                {
                    t.Value();
                    Console.WriteLine("ok      " + t.Key);
                }
                catch (Exception ex)
                {
                    echecs++;
                    Console.WriteLine("ÉCHEC   " + t.Key + " : " + ex.Message);
                }
            }
            Console.WriteLine(echecs == 0 ? "Tous les tests passent (" + tests.Count + ")." : echecs + " test(s) en échec.");
            return echecs == 0 ? 0 : 1;
        }

        private static KeyValuePair<string, Action> T(string nom, Action a) { return new KeyValuePair<string, Action>(nom, a); }

        private static void Egal<T2>(T2 attendu, T2 obtenu, string quoi)
        {
            if (!EqualityComparer<T2>.Default.Equals(attendu, obtenu))
                throw new Exception(quoi + " : attendu « " + attendu + " », obtenu « " + obtenu + " »");
        }

        private static void Vrai(bool condition, string quoi) { if (!condition) throw new Exception(quoi); }

        // ---------- Tests ----------

        private static void NomHote()
        {
            Egal("app.hubspot.com", Domaines.NomHote("https://App.HubSpot.com:443/contacts/123?x=1#y"), "adresse complète");
            Egal("notion.so", Domaines.NomHote("https://www.notion.so/Mon-espace-abc123"), "www retiré");
            Egal<string>(null, Domaines.NomHote("chrome://settings"), "page interne");
            Egal<string>(null, Domaines.NomHote("file:///C:/Users/Jean/doc.pdf"), "fichier local");
            Egal<string>(null, Domaines.NomHote("http://192.168.1.1/admin"), "adresse IP");
            Egal<string>(null, Domaines.NomHote("http://localhost:3000/"), "localhost");
            Egal<string>(null, Domaines.NomHote("http://nas.local/"), "réseau local");
            Egal<string>(null, Domaines.NomHote("chrome-extension://abcdef/popup.html"), "extension");
            Egal("xn--caf-dma.fr", Domaines.NomHote("https://café.fr/menu"), "nom accentué en ASCII");
        }

        private static void Principal()
        {
            Egal("monoutil.com", Domaines.Principal("client42.monoutil.com"), "sous-domaine de compte");
            Egal("exemple.fr", Domaines.Principal("exemple.fr"), "déjà principal");
            Egal("mairie.gouv.fr", Domaines.Principal("services.mairie.gouv.fr"), "second niveau français");
            Egal("exemple.co.uk", Domaines.Principal("app.exemple.co.uk"), "second niveau britannique");
        }

        private static Catalogue Cat() { return Catalogue.Embarque(); }

        private static void ReconnaissanceDomaines()
        {
            var c = Cat();
            Vrai(c.Outils.Count >= 250, "catalogue embarqué trop court : " + c.Outils.Count);
            Egal("hubspot", c.ReconnaitreDomaine("app.hubspot.com").Id, "sous-domaine");
            Egal("notion", c.ReconnaitreDomaine("notion.so").Id, "exact");
            Egal("gmail", c.ReconnaitreDomaine("mail.google.com").Id, "suite Google");
            Egal<Outil>(null, c.ReconnaitreDomaine("fakenotion.so"), "faux ami");
            Egal<Outil>(null, c.ReconnaitreDomaine("notion.so.exemple.com"), "domaine piège");
            string domaine;
            c.ReconnaitreDomaine("mon-equipe.slack.com", out domaine);
            Egal("slack.com", domaine, "le domaine gardé est celui du catalogue, pas le sous-domaine du compte");
        }

        private static void ReconnaissanceProgrammes()
        {
            var c = Cat();
            Egal("zoom", c.ReconnaitreProgramme("Zoom Workplace (64-bit)").Id, "suffixe");
            Egal("slack", c.ReconnaitreProgramme("SLACK").Id, "majuscules");
            Egal<Outil>(null, c.ReconnaitreProgramme("Slackware"), "nom plus long");
            Egal("grammarly", c.ReconnaitreProgramme("Grammarly: AI Writing").Id, "nom d'extension");
            Egal("whatsapp", c.ReconnaitreProgramme("WhatsApp").Id, "exact");
        }

        private static void Executable()
        {
            Egal("slack.exe", Environnement.Executable("\"C:\\Users\\Jean\\AppData\\Local\\slack\\slack.exe\",0"), "icône");
            Egal("dropbox.exe", Environnement.Executable("\"C:\\Program Files\\Dropbox\\Client\\Dropbox.exe\" /systemstartup"), "commande");
            Egal("teams.exe", Environnement.Executable("C:\\Program Files\\Teams\\Teams.exe --process-start-args"), "sans guillemets");
            Egal<string>(null, Environnement.Executable("C:\\Windows\\system32\\shell32.dll,-21"), "pas un .exe");
            Egal<string>(null, Environnement.Executable(""), "vide");
        }

        private static void NavigateursTrouves()
        {
            using (var pc = new PcFictif())
            {
                var profils = Navigateurs.Trouver(pc.Env);
                var noms = profils.Select(p => p.Navigateur + ":" + Path.GetFileName(p.Dossier)).OrderBy(x => x).ToList();
                string liste = string.Join(", ", noms);
                foreach (string attendu in new[] { "chrome:Default", "chrome:Profile 1", "edge:Default", "opera:Opera Stable", "thorium:Default", "firefox:abc.default-release" })
                    Vrai(noms.Contains(attendu), "profil manquant " + attendu + " dans " + liste);
                Vrai(!profils.Any(p => p.Dossier.ToLowerInvariant().Contains("thunderbird")), "Thunderbird n'est pas un navigateur : " + liste);
                Vrai(!profils.Any(p => p.Navigateur == "microsoft" || p.Navigateur == "slack"), "fenêtre web intégrée ou application prise pour un navigateur : " + liste);
                Egal(6, profils.Count, "nombre de profils (" + liste + ")");
            }
        }

        private static void ReleveComplet()
        {
            using (var pc = new PcFictif())
            {
                var r = Releve.Faire(pc.Env, Cat());
                Func<string, Element> site = d => r.Elements.FirstOrDefault(e => e.Source == Source.Navigateur && e.Domaine == d);

                var notion = site("notion.so");
                Vrai(notion != null && notion.Coche, "Notion reconnu et coché");
                Egal(13L, notion.Visites, "visites de Notion, tous profils et navigateurs");
                Egal("chrome,edge", string.Join(",", notion.NavigateursVus), "navigateurs de Notion");
                Vrai(site("hubspot.com") != null && site("hubspot.com").Coche, "HubSpot reconnu par app.hubspot.com");
                Vrai(site("app.hubspot.com") == null, "pas de sous-domaine gardé pour un outil reconnu");
                Vrai(site("trello.com") != null, "Opera lu");
                Vrai(site("calendly.com") != null && site("calendly.com").NavigateursVus.Contains("thorium"), "navigateur inconnu repéré par ses fichiers");
                Vrai(site("lastpass.com") != null && site("lastpass.com").NavigateursVus.Contains("firefox"), "Firefox lu");
                Vrai(site("slack.com") == null, "visite de plus de 90 jours ignorée");

                var perso = site("exemple-perso.fr");
                Vrai(perso != null && !perso.Coche, "site fréquent inconnu proposé décoché");
                Vrai(site("rare.fr") == null, "site inconnu peu visité non proposé");
                Vrai(site("monoutil.com") != null && site("client42.monoutil.com") == null, "site inconnu réduit au domaine principal");
                Vrai(!r.Elements.Any(e => e.Domaine != null && e.Domaine.Contains("thunderbird-perso")), "historique de Thunderbird ignoré");

                Func<Source, string, Element> nom = (s, n) => r.Elements.FirstOrDefault(e => e.Source == s && e.Nom == n);
                var slack = nom(Source.Programme, "Slack");
                Vrai(slack != null && slack.Coche && slack.Executable == "slack.exe", "Slack installé, reconnu, avec son .exe seul");
                Vrai(nom(Source.Programme, "Microsoft Visual C++ 2015-2022 Redistributable (x64)") == null, "composant technique écarté");
                var maison = nom(Source.Programme, "Compta Maison 2024");
                Vrai(maison != null && !maison.Coche, "logiciel inconnu proposé décoché");
                Vrai(nom(Source.Programme, "WhatsApp") != null, "application du Store reconnue");
                Vrai(!r.Elements.Any(e => e.Nom != null && e.Nom.Contains("BingWeather")), "composant du Store écarté");
                Vrai(nom(Source.Demarrage, "Dropbox") != null && nom(Source.Demarrage, "Dropbox").Coche, "Dropbox au démarrage");
                Vrai(nom(Source.Demarrage, "Zoom") != null, "raccourci du dossier Démarrage");

                Vrai(nom(Source.Extension, "Grammarly: AI Writing") != null, "extension Chrome");
                Vrai(nom(Source.Extension, "Bitwarden Password Manager") != null, "extension au nom traduit");
                Vrai(nom(Source.Extension, "Thème sombre") == null, "thème écarté");
                Vrai(nom(Source.Extension, "uBlock Origin") != null && !nom(Source.Extension, "uBlock Origin").Coche, "extension Firefox inconnue décochée");
                Egal(0, r.NavigateursIllisibles.Count, "aucun historique illisible");
            }
        }

        private static void HistoriqueOuvert()
        {
            using (var pc = new PcFictif())
            {
                // Le navigateur ouvert garde le fichier en lecture-écriture et partage la lecture.
                using (new FileStream(pc.HistoriqueChrome, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    var r = Releve.Faire(pc.Env, Cat());
                    Egal(0, r.NavigateursIllisibles.Count, "historique lisible pendant que le navigateur est ouvert");
                }
                Vrai(!Directory.GetDirectories(Path.GetTempPath(), "ergalo-releve-*").Any(), "les copies d'historique sont effacées");
            }
        }

        private static void HistoriqueVerrouille()
        {
            using (var pc = new PcFictif())
            using (new FileStream(pc.HistoriqueChrome, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var r = Releve.Faire(pc.Env, Cat());
                Vrai(r.NavigateursIllisibles.Contains("chrome"), "Chrome signalé illisible");
                Vrai(r.Elements.Any(e => e.Domaine == "trello.com"), "les autres navigateurs sont lus");
            }
        }

        /// <summary>Un identifiant de marque comme ceux du site : 43 caractères base64url.</summary>
        private const string IdEssai = "Essai_marque-0123456789abcdefghijklmnopqrst";

        private static string NomMarque(string jour, string id) { return "ergalo-releve-" + jour + "-" + id + ".exe"; }

        private static Marque MarqueEssai(DateTime expire)
        {
            return Marque.LireNom(NomMarque(expire.ToString("yyyyMMdd"), IdEssai));
        }

        private static void ExportFichier()
        {
            using (var pc = new PcFictif())
            {
                var r = Releve.Faire(pc.Env, Cat());
                var perso = r.Elements.First(e => e.Domaine == "exemple-perso.fr");
                perso.Coche = true; // la personne coche un site inconnu
                r.Elements.First(e => e.Domaine == "notion.so").Coche = false; // et décoche Notion

                string json = Export.Json(MarqueEssai(Maintenant.AddDays(30)), r.Elements, new DateTimeOffset(2026, 10, 29, 15, 40, 0, TimeSpan.FromHours(1)));
                var d = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
                Egal("ergalo-scanner", (string)d["format"], "format");
                Egal(1, (int)d["version"], "version");
                Egal("2026-10-29T15:40:00+01:00", (string)d["cree_le"], "date");
                var m = (Dictionary<string, object>)d["marque"];
                Egal(IdEssai, (string)m["id"], "marque recopiée");
                Egal("ergalo-marque", (string)m["format"], "format de la marque");
                Egal(1, (int)m["version"], "version de la marque");

                var elements = ((object[])d["elements"]).Cast<Dictionary<string, object>>().ToList();
                Vrai(elements.Any(e => (string)e["source"] == "navigateur" && (string)e["domaine"] == "exemple-perso.fr"), "site coché présent");
                Vrai(!elements.Any(e => e.ContainsKey("domaine") && (string)e["domaine"] == "notion.so"), "site décoché absent");
                Vrai(!elements.Any(e => e.ContainsKey("nom") && (string)e["nom"] == "Compta Maison 2024"), "logiciel décoché absent");
                var hubspot = elements.First(e => e.ContainsKey("outil") && (string)e["outil"] == "hubspot");
                Egal("hubspot.com", (string)hubspot["domaine"], "domaine d'outil");
                Vrai(hubspot["visites_90j"] is int, "visites en nombre");
                Vrai(elements.Any(e => (string)e["source"] == "extension" && (string)e["navigateur"] == "chrome"), "extension avec son navigateur");
                Vrai(elements.Any(e => (string)e["source"] == "demarrage" && (string)e["programme"] == "dropbox.exe"), "démarrage avec son .exe");

                foreach (string interdit in new[] { "Users", "Jean", "AppData", "/contacts", "http", "Mon-espace", "Profile 1" })
                    Vrai(!json.Contains(interdit), "le fichier contient « " + interdit + " »");
                Vrai(Encoding.UTF8.GetByteCount(json) < 2 * 1024 * 1024, "moins de 2 Mo");
            }
        }

        private static void MarqueLue()
        {
            Egal(43, IdEssai.Length, "identifiant d'essai de 43 caractères");
            Vrai(MarqueEssai(Maintenant.AddDays(1)) != null, "marque valide lue");
            Egal(IdEssai, MarqueEssai(Maintenant.AddDays(1)).Id, "identifiant lu");
            Vrai(!MarqueEssai(Maintenant).Expiree(Maintenant), "valable tout le dernier jour");
            Vrai(!MarqueEssai(Maintenant.AddDays(1)).Expiree(Maintenant), "pas encore expirée");
            Vrai(MarqueEssai(Maintenant.AddDays(-1)).Expiree(Maintenant), "expirée");
            Vrai(Marque.LireNom(NomMarque("20261128", IdEssai).Replace(".exe", " (1).exe")) != null, "copie renommée par le navigateur");
            Vrai(Marque.LireNom(NomMarque("20261128", IdEssai).Replace(".exe", "(2).exe")) != null, "copie renommée par Firefox");
            Vrai(Marque.LireNom("ergalo-releve.exe") == null, "sans marque");
            Vrai(Marque.LireNom("mon-programme.exe") == null, "renommé");
            Vrai(Marque.LireNom(NomMarque("20261128", IdEssai.Substring(1))) == null, "identifiant tronqué");
            Vrai(Marque.LireNom(NomMarque("20261128", IdEssai + "x")) == null, "identifiant allongé");
            Vrai(Marque.LireNom(NomMarque("20261332", IdEssai)) == null, "date impossible");
            Vrai(Marque.LireNom(null) == null, "nom absent");
            string json = MarqueEssai(new DateTime(2026, 11, 28)).Json;
            Egal("{\"format\":\"ergalo-marque\",\"version\":1,\"id\":\"" + IdEssai + "\",\"expire_le\":\"2026-11-28\"}", json, "marque recopiée dans le fichier");
        }

        private static void ProgrammeComplet()
        {
            if (exe == null || !File.Exists(exe)) throw new Exception("chemin du programme non donné");
            using (var pc = new PcFictif())
            {
                string dossier = pc.Racine;
                string sansMarque = Copier(exe, dossier, "ergalo-releve.exe");
                Egal(2, Lancer(sansMarque, pc, Path.Combine(dossier, "a.json")), "code de sortie sans marque");
                Vrai(!File.Exists(Path.Combine(dossier, "a.json")), "aucun fichier sans marque");

                string renomme = Copier(exe, dossier, "releve.exe");
                Egal(2, Lancer(renomme, pc, Path.Combine(dossier, "a2.json")), "code de sortie renommé");

                string expire = Copier(exe, dossier, NomMarque("20261001", IdEssai));
                Egal(3, Lancer(expire, pc, Path.Combine(dossier, "b.json")), "code de sortie expiré");

                string valide = Copier(exe, dossier, NomMarque("20261128", IdEssai).Replace(".exe", " (1).exe"));
                string sortie = Path.Combine(dossier, "c.json");
                Egal(0, Lancer(valide, pc, sortie), "code de sortie avec marque valide");
                string json = File.ReadAllText(sortie, Encoding.UTF8);
                Vrai(json.Contains("\"id\":\"" + IdEssai + "\""), "marque recopiée");
                Vrai(json.Contains("\"domaine\": \"notion.so\""), "Notion dans le fichier");
                Vrai(!json.Contains("exemple-perso.fr"), "site inconnu non coché absent");
                Vrai(File.ReadAllBytes(valide).SequenceEqual(File.ReadAllBytes(exe)), "le programme marqué est octet pour octet le programme publié");
            }
        }

        /// <summary>Copie du programme sous le nom que lui donne le site au téléchargement : son contenu ne change pas.</summary>
        public static string Copier(string source, string dossier, string nom)
        {
            string cible = Path.Combine(dossier, nom);
            File.Copy(source, cible, true);
            return cible;
        }

        private static int Lancer(string programme, PcFictif pc, string sortie)
        {
            var info = new ProcessStartInfo(programme, "--essai \"" + pc.Racine + "\" --sortie \"" + sortie + "\" --maintenant 2026-10-29T15:00:00+01:00")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using (var p = Process.Start(info))
            {
                if (!p.WaitForExit(60000)) { p.Kill(); throw new Exception("le programme ne s'arrête pas"); }
                return p.ExitCode;
            }
        }
    }

    /// <summary>Un PC fictif dans un dossier temporaire, effacé à la fin du test.</summary>
    internal sealed class PcFictif : IDisposable
    {
        public readonly string Racine;
        public readonly Environnement Env;
        public string HistoriqueChrome;

        private static readonly DateTime Maintenant = new DateTime(2026, 10, 29, 14, 0, 0, DateTimeKind.Utc);

        public PcFictif() : this(Path.Combine(Path.GetTempPath(), "ergalo-pc-fictif-" + Guid.NewGuid().ToString("N").Substring(0, 8)))
        {
        }

        public PcFictif(string racine)
        {
            Racine = racine;
            string local = Path.Combine(Racine, @"AppData\Local");
            string roaming = Path.Combine(Racine, @"AppData\Roaming");

            // Chrome : deux profils, des extensions.
            string chrome = Path.Combine(local, @"Google\Chrome\User Data");
            Fichier(Path.Combine(chrome, "Local State"), "{}");
            HistoriqueChrome = Chromium(Path.Combine(chrome, "Default"), new[]
            {
                V("https://www.notion.so/Mon-espace-abc123", 0, 10),
                V("https://app.hubspot.com/contacts/123/record/0-1/456", 3, 3),
                V("https://mail.google.com/mail/u/0/#inbox", 1, 5),
                V("https://exemple-perso.fr/blog/article", 2, 7),
                V("https://rare.fr/", 4, 2),
                V("https://client42.monoutil.com/tableau", 5, 4),
                V("https://www.monoutil.com/", 6, 2),
                V("https://slack.com/intl/fr-fr/", 100, 20),
                V("chrome://settings/", 0, 3),
                V("http://192.168.1.1/admin", 0, 4),
            });
            Chromium(Path.Combine(chrome, "Profile 1"), new[] { V("https://notion.so/autre", 1, 2) });
            Extension(Path.Combine(chrome, @"Default\Extensions\kbfnbcaeplbcioakkpcpgfkobkghlhen\14.1.0_0"), "{\"name\":\"Grammarly: AI Writing\",\"manifest_version\":3}", null);
            Extension(Path.Combine(chrome, @"Default\Extensions\nngceckbapebfimnlniiiahkandclblb\2024.10.0_0"), "{\"name\":\"__MSG_extName__\",\"default_locale\":\"en\"}",
                "{\"extName\":{\"message\":\"Bitwarden Password Manager\"}}");
            Extension(Path.Combine(chrome, @"Default\Extensions\aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\1.0_0"), "{\"name\":\"Thème sombre\",\"theme\":{}}", null);

            // Edge, Opera, et un navigateur Chromium que la liste ne connaît pas.
            Chromium(Path.Combine(local, @"Microsoft\Edge\User Data\Default"), new[] { V("https://notion.so/x", 0, 1) });
            Fichier(Path.Combine(local, @"Microsoft\Edge\User Data\Local State"), "{}");
            Chromium(Path.Combine(roaming, @"Opera Software\Opera Stable"), new[] { V("https://trello.com/b/xyz/tableau", 0, 6) });
            Fichier(Path.Combine(roaming, @"Opera Software\Opera Stable\Local State"), "{}");
            Fichier(Path.Combine(local, @"Thorium\User Data\Local State"), "{}");
            Chromium(Path.Combine(local, @"Thorium\User Data\Default"), new[] { V("https://calendly.com/equipe/30min", 0, 3) });

            // Fenêtres web intégrées et application de bureau : même forme qu'un navigateur, mais n'en sont pas.
            Fichier(Path.Combine(local, @"Microsoft\Olk\EBWebView\Local State"), "{}");
            Chromium(Path.Combine(local, @"Microsoft\Olk\EBWebView\Default"), new[] { V("https://integre-outlook.fr/", 0, 30) });
            Fichier(Path.Combine(local, @"Microsoft\Copilot\User Data\Local State"), "{}");
            Chromium(Path.Combine(local, @"Microsoft\Copilot\User Data\Default"), new[] { V("https://integre-copilot.fr/", 0, 30) });
            Fichier(Path.Combine(roaming, @"Slack\Local State"), "{}");
            Chromium(Path.Combine(roaming, "Slack"), new[] { V("https://integre-slack.fr/", 0, 30) });

            // Firefox, et Thunderbird qui a la même forme mais n'est pas un navigateur.
            string firefox = Path.Combine(roaming, @"Mozilla\Firefox");
            Fichier(Path.Combine(firefox, "profiles.ini"), "[Profile0]\nPath=Profiles/abc.default-release\n");
            Firefox(Path.Combine(firefox, @"Profiles\abc.default-release"), new[] { V("https://lastpass.com/vault/", 0, 8) });
            Fichier(Path.Combine(firefox, @"Profiles\abc.default-release\extensions.json"),
                "{\"addons\":[{\"type\":\"extension\",\"location\":\"app-profile\",\"defaultLocale\":{\"name\":\"uBlock Origin\"}}," +
                "{\"type\":\"extension\",\"location\":\"app-system-defaults\",\"defaultLocale\":{\"name\":\"Module interne\"}}]}");
            string thunderbird = Path.Combine(roaming, "Thunderbird");
            Fichier(Path.Combine(thunderbird, "profiles.ini"), "[Profile0]\n");
            Firefox(Path.Combine(thunderbird, @"Profiles\x.default"), new[] { V("https://thunderbird-perso.fr/", 0, 50) });

            // Registre simulé et dossier Démarrage.
            Fichier(Path.Combine(Racine, "registre.tsv"), string.Join("\n", new[]
            {
                "installe\tSlack\t\"C:\\Users\\Jean\\AppData\\Local\\slack\\slack.exe\",0",
                "installe\tMicrosoft Visual C++ 2015-2022 Redistributable (x64)\t",
                "installe\tCompta Maison 2024\tC:\\Program Files\\ComptaMaison\\compta.exe",
                "demarrage\tDropbox\t\"C:\\Program Files\\Dropbox\\Client\\Dropbox.exe\" /systemstartup",
                "demarrage\tSecurityHealth\t%windir%\\system32\\SecurityHealthSystray.exe",
                "paquet\t5319275A.WhatsAppDesktop_2.2440.9.0_x64__cv1g1gvanyjgm",
                "paquet\tMicrosoft.BingWeather_4.53.0_x64__8wekyb3d8bbwe",
            }));
            Fichier(Path.Combine(Racine, @"Demarrage\Zoom.lnk"), "");
            Fichier(Path.Combine(Racine, @"Demarrage\desktop.ini"), "");

            Env = new EnvironnementEssai(Racine, Maintenant);
        }

        public void Dispose()
        {
            try { Directory.Delete(Racine, true); } catch (Exception) { }
        }

        private static object[] V(string adresse, int ilYaJours, int visites) { return new object[] { adresse, ilYaJours, visites }; }

        private static void Fichier(string chemin, string contenu)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(chemin));
            File.WriteAllText(chemin, contenu, new UTF8Encoding(false));
        }

        private static void Extension(string dossier, string manifeste, string messagesEn)
        {
            Fichier(Path.Combine(dossier, "manifest.json"), manifeste);
            if (messagesEn != null) Fichier(Path.Combine(dossier, @"_locales\en\messages.json"), messagesEn);
        }

        private static string Chromium(string profil, object[][] visites)
        {
            Directory.CreateDirectory(profil);
            string chemin = Path.Combine(profil, "History");
            var sql = new StringBuilder("CREATE TABLE urls(id INTEGER PRIMARY KEY, url TEXT, title TEXT, visit_count INTEGER, last_visit_time INTEGER);" +
                                        "CREATE TABLE visits(id INTEGER PRIMARY KEY, url INTEGER, visit_time INTEGER);");
            long epoque = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
            for (int i = 0; i < visites.Length; i++)
            {
                sql.AppendFormat("INSERT INTO urls VALUES({0}, '{1}', 'Titre privé', {2}, 0);", i + 1, visites[i][0], visites[i][2]);
                long t = (Maintenant.AddDays(-(int)visites[i][1]).AddHours(-1).Ticks - epoque) / 10;
                for (int k = 0; k < (int)visites[i][2]; k++) sql.AppendFormat("INSERT INTO visits(url, visit_time) VALUES({0}, {1});", i + 1, t - k);
            }
            SqliteEcriture.Executer(chemin, sql.ToString());
            return chemin;
        }

        private static void Firefox(string profil, object[][] visites)
        {
            Directory.CreateDirectory(profil);
            var sql = new StringBuilder("PRAGMA journal_mode=WAL;CREATE TABLE moz_places(id INTEGER PRIMARY KEY, url TEXT, title TEXT);" +
                                        "CREATE TABLE moz_historyvisits(id INTEGER PRIMARY KEY, place_id INTEGER, visit_date INTEGER);");
            long epoque = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
            for (int i = 0; i < visites.Length; i++)
            {
                sql.AppendFormat("INSERT INTO moz_places VALUES({0}, '{1}', 'Titre privé');", i + 1, visites[i][0]);
                long t = (Maintenant.AddDays(-(int)visites[i][1]).AddHours(-1).Ticks - epoque) / 10;
                for (int k = 0; k < (int)visites[i][2]; k++) sql.AppendFormat("INSERT INTO moz_historyvisits(place_id, visit_date) VALUES({0}, {1});", i + 1, t - k);
            }
            SqliteEcriture.Executer(Path.Combine(profil, "places.sqlite"), sql.ToString());
        }
    }

    /// <summary>Écriture SQLite, réservée aux tests (le programme, lui, ne fait que lire).</summary>
    internal static class SqliteEcriture
    {
        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int sqlite3_open_v2(byte[] nom, out IntPtr db, int options, IntPtr vfs);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int sqlite3_exec(IntPtr db, byte[] sql, IntPtr rappel, IntPtr arg, IntPtr erreur);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern int sqlite3_close_v2(IntPtr db);

        public static void Executer(string chemin, string sql)
        {
            IntPtr db;
            if (sqlite3_open_v2(Encoding.UTF8.GetBytes(chemin + "\0"), out db, 0x2 | 0x4, IntPtr.Zero) != 0) throw new Exception("création de " + chemin);
            try
            {
                int rc = sqlite3_exec(db, Encoding.UTF8.GetBytes(sql + "\0"), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (rc != 0) throw new Exception("SQL refusé (" + rc + ")");
            }
            finally { sqlite3_close_v2(db); }
        }
    }
}
