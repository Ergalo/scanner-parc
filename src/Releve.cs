// Le relevé : tout ce qui a été trouvé sur le PC, sous forme d'éléments à cocher. Un outil reconnu est coché d'office ;
// ce qui n'est pas reconnu est proposé décoché, pour que la personne choisisse elle-même ce qui part.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ergalo.Releve
{
    internal enum Source { Programme, Demarrage, Extension, Navigateur }

    internal sealed class Element
    {
        public Source Source;
        public string Nom;          // programme, démarrage, extension
        public string Executable;   // programme, démarrage : nom du fichier .exe seul
        public string Navigateur;   // extension
        public string Domaine;      // navigateur
        public long Visites;        // navigateur, sur 90 jours
        public SortedSet<string> NavigateursVus = new SortedSet<string>(StringComparer.Ordinal);
        public Outil Outil;
        public bool Coche;
    }

    internal sealed class Releve
    {
        public const int Jours = 90;
        public const int SeuilSiteInconnu = 5;      // visites sur 90 jours pour proposer un site inconnu
        public const int MaxSitesInconnus = 150;

        public readonly List<Element> Elements = new List<Element>();
        public readonly List<string> NavigateursTrouves = new List<string>();
        public readonly List<string> NavigateursIllisibles = new List<string>(); // historique illisible (navigateur à fermer)

        // Logiciels techniques qui ne sont pas des outils de travail : on ne les propose pas, même décochés.
        private static readonly Regex Technique = new Regex(
            @"redistributable|runtime|\.net |\.net$|framework|driver|pilote|sdk|webview|update|mise à jour|visual c\+\+|" +
            @"^intel|^nvidia|^amd |^realtek|^microsoft edge|^windows |^microsoft windows|bonjour|vcpp|directx|" +
            @"launcher|helper|service pack|^kb\d|language pack|module linguistique|^python \d|^node\.js|^java\b",
            RegexOptions.IgnoreCase);

        // Paquets du Store dont le nom ne suit pas celui du catalogue.
        private static readonly Dictionary<string, string> AliasPaquets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "MSTeams", "Microsoft Teams" },
            { "MicrosoftTeams", "Microsoft Teams" },
            { "OutlookForWindows", "Outlook" },
            { "Todos", "Microsoft To Do" },
            { "Office.OneNote", "OneNote" },
            { "SpotifyMusic", "Spotify" },
        };

        public static Releve Faire(Environnement env, Catalogue catalogue)
        {
            var r = new Releve();
            r.Programmes(env, catalogue);
            r.Demarrage(env, catalogue);
            r.Web(env, catalogue);
            return r;
        }

        private void Programmes(Environnement env, Catalogue catalogue)
        {
            var vus = new HashSet<string>();
            foreach (var p in env.ProgrammesInstalles())
            {
                string cle = Catalogue.Normaliser(p.Nom);
                if (!vus.Add(cle)) continue;
                Outil outil = catalogue.ReconnaitreProgramme(p.Nom);
                if (outil == null && Technique.IsMatch(p.Nom)) continue;
                Elements.Add(new Element { Source = Source.Programme, Nom = p.Nom, Executable = p.Executable, Outil = outil, Coche = outil != null });
            }
            // Applications du Store : seules celles du catalogue sont proposées (les autres sont surtout des composants de Windows).
            foreach (string paquet in env.PaquetsStore())
            {
                string nom = NomPaquet(paquet);
                if (nom == null) continue;
                Outil outil = ReconnaitrePaquet(catalogue, nom);
                if (outil == null || !vus.Add(Catalogue.Normaliser(outil.Nom))) continue;
                if (Elements.Any(e => e.Source == Source.Programme && e.Outil == outil)) continue;
                Elements.Add(new Element { Source = Source.Programme, Nom = outil.Nom, Outil = outil, Coche = true });
            }
        }

        /// <summary>« 5319275A.WhatsAppDesktop_2.2440.9.0_x64__cv1g1gvanyjgm » donne « WhatsAppDesktop ».</summary>
        public static string NomPaquet(string paquet)
        {
            string sansVersion = paquet.Split('_')[0];
            int point = sansVersion.IndexOf('.');
            string nom = point >= 0 ? sansVersion.Substring(point + 1) : sansVersion;
            return nom.Length == 0 ? null : nom;
        }

        private static Outil ReconnaitrePaquet(Catalogue catalogue, string nom)
        {
            string alias;
            if (AliasPaquets.TryGetValue(nom, out alias)) return catalogue.ReconnaitreProgramme(alias);
            Outil outil = catalogue.ReconnaitreProgramme(nom);
            if (outil != null) return outil;
            // « WhatsAppDesktop » : le nom du catalogue, sans espace, en début de nom du paquet.
            string compact = Catalogue.Normaliser(nom).Replace(" ", "");
            Outil meilleur = null;
            int longueur = 0;
            foreach (Outil o in catalogue.Outils.Values)
            {
                string n = Catalogue.Normaliser(o.Nom).Replace(" ", "");
                if (n.Length >= 4 && n.Length > longueur && compact.StartsWith(n, StringComparison.Ordinal)) { meilleur = o; longueur = n.Length; }
            }
            return meilleur;
        }

        private void Demarrage(Environnement env, Catalogue catalogue)
        {
            var lancements = new List<Lancement>(env.LancementsRegistre());
            foreach (string dossier in env.DossiersDemarrage)
            {
                try
                {
                    if (!System.IO.Directory.Exists(dossier)) continue;
                    foreach (string f in System.IO.Directory.GetFiles(dossier))
                    {
                        string ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                        if (ext == ".ini") continue;
                        lancements.Add(new Lancement { Nom = System.IO.Path.GetFileNameWithoutExtension(f), Executable = ext == ".exe" ? System.IO.Path.GetFileName(f).ToLowerInvariant() : null });
                    }
                }
                catch (Exception) { }
            }
            var vus = new HashSet<string>();
            foreach (var l in lancements)
            {
                if (!vus.Add(Catalogue.Normaliser(l.Nom))) continue;
                Outil outil = catalogue.ReconnaitreProgramme(l.Nom);
                if (outil == null && l.Executable != null) outil = catalogue.ReconnaitreProgramme(l.Executable.Substring(0, l.Executable.Length - 4));
                if (outil == null && Technique.IsMatch(l.Nom)) continue;
                Elements.Add(new Element { Source = Source.Demarrage, Nom = l.Nom, Executable = l.Executable, Outil = outil, Coche = outil != null });
            }
        }

        private void Web(Environnement env, Catalogue catalogue)
        {
            var reconnus = new Dictionary<string, Element>();   // par domaine du catalogue
            var inconnus = new Dictionary<string, Element>();   // par domaine principal
            var extensions = new Dictionary<string, Element>();

            foreach (Profil profil in Navigateurs.Trouver(env))
            {
                if (!NavigateursTrouves.Contains(profil.Navigateur)) NavigateursTrouves.Add(profil.Navigateur);

                Dictionary<string, long> visites;
                try { visites = Navigateurs.Historique(profil, env.Maintenant, Jours); }
                catch (Exception)
                {
                    if (!NavigateursIllisibles.Contains(profil.Navigateur)) NavigateursIllisibles.Add(profil.Navigateur);
                    visites = new Dictionary<string, long>();
                }
                foreach (var kv in visites)
                {
                    string domaine;
                    Outil outil = catalogue.ReconnaitreDomaine(kv.Key, out domaine);
                    var table = outil != null ? reconnus : inconnus;
                    string cle = outil != null ? domaine : Domaines.Principal(kv.Key);
                    Element e;
                    if (!table.TryGetValue(cle, out e))
                    {
                        e = new Element { Source = Source.Navigateur, Domaine = cle, Outil = outil, Coche = outil != null };
                        table[cle] = e;
                    }
                    e.Visites += kv.Value;
                    e.NavigateursVus.Add(profil.Navigateur);
                }

                foreach (string nom in Navigateurs.Extensions(profil))
                {
                    string cle = profil.Navigateur + "\t" + Catalogue.Normaliser(nom);
                    if (extensions.ContainsKey(cle)) continue;
                    Outil outil = catalogue.ReconnaitreProgramme(nom);
                    extensions[cle] = new Element { Source = Source.Extension, Nom = nom, Navigateur = profil.Navigateur, Outil = outil, Coche = outil != null };
                }
            }

            Elements.AddRange(reconnus.Values.OrderByDescending(e => e.Visites));
            Elements.AddRange(inconnus.Values.Where(e => e.Visites >= SeuilSiteInconnu).OrderByDescending(e => e.Visites).Take(MaxSitesInconnus));
            Elements.AddRange(extensions.Values);
        }
    }
}
