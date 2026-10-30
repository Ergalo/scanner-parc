// Catalogue réduit embarqué dans le programme : familles, outils, domaines et noms de programmes. Il est produit à partir
// du catalogue des outils d'Ergalo (catalogue.tsv, refait par le site : npm run scanner:catalogue). La reconnaissance suit
// exactement les règles du site ; la page du questionnaire refait de toute façon la sienne, avec le catalogue du jour.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Ergalo.Releve
{
    internal sealed class Outil
    {
        public string Id;
        public string Nom;
        public string Famille;
    }

    internal sealed class Catalogue
    {
        public readonly List<KeyValuePair<string, string>> Familles = new List<KeyValuePair<string, string>>();
        public readonly Dictionary<string, Outil> Outils = new Dictionary<string, Outil>();
        private readonly Dictionary<string, Outil> domaines = new Dictionary<string, Outil>();
        private readonly List<KeyValuePair<string, Outil>> programmes = new List<KeyValuePair<string, Outil>>();

        public static Catalogue Embarque()
        {
            using (Stream flux = typeof(Catalogue).Assembly.GetManifestResourceStream("catalogue.tsv"))
            {
                if (flux == null) throw new InvalidOperationException("Catalogue absent du programme");
                using (var lecteur = new StreamReader(flux, Encoding.UTF8)) return Lire(lecteur.ReadToEnd());
            }
        }

        /// <summary>Lignes F (famille), O (outil), D (domaine), P (programme), champs séparés par des tabulations.</summary>
        public static Catalogue Lire(string texte)
        {
            var c = new Catalogue();
            foreach (string brute in texte.Split('\n'))
            {
                string ligne = brute.TrimEnd('\r');
                if (ligne.Length == 0 || ligne[0] == '#') continue;
                string[] champs = ligne.Split('\t');
                switch (champs[0])
                {
                    case "F":
                        c.Familles.Add(new KeyValuePair<string, string>(champs[1], champs[2]));
                        break;
                    case "O":
                        c.Outils[champs[1]] = new Outil { Id = champs[1], Nom = champs[2], Famille = champs[3] };
                        break;
                    case "D":
                        if (!c.domaines.ContainsKey(champs[1])) c.domaines[champs[1]] = c.Outils[champs[2]];
                        break;
                    case "P":
                        string nom = Normaliser(champs[1]);
                        if (nom.Length > 0) c.programmes.Add(new KeyValuePair<string, Outil>(nom, c.Outils[champs[2]]));
                        break;
                }
            }
            // Du nom le plus long au plus court : le plus précis gagne.
            c.programmes.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
            return c;
        }

        public string LibelleFamille(string id)
        {
            foreach (var f in Familles) if (f.Key == id) return f.Value;
            return id;
        }

        /// <summary>Correspondance exacte, ou sous-domaine à une frontière de nom ; le domaine le plus long gagne.</summary>
        public Outil ReconnaitreDomaine(string hote)
        {
            string domaine;
            return ReconnaitreDomaine(hote, out domaine);
        }

        /// <summary>Même chose, et <paramref name="domaine"/> reçoit le domaine du catalogue reconnu (jamais le sous-domaine du compte).</summary>
        public Outil ReconnaitreDomaine(string hote, out string domaine)
        {
            domaine = null;
            if (string.IsNullOrEmpty(hote)) return null;
            string[] etiquettes = hote.Split('.');
            for (int i = 0; i < etiquettes.Length - 1; i++)
            {
                Outil outil;
                string candidat = string.Join(".", etiquettes, i, etiquettes.Length - i);
                if (domaines.TryGetValue(candidat, out outil))
                {
                    domaine = candidat;
                    return outil;
                }
            }
            return null;
        }

        /// <summary>
        /// L'outil dont un programme est le début du nom affiché, suivi de la fin du nom ou d'un caractère qui n'est ni une
        /// lettre ni un chiffre : « Zoom Workplace (64-bit) » reconnaît Zoom, « Slackware » ne reconnaît pas Slack.
        /// </summary>
        public Outil ReconnaitreProgramme(string nomAffiche)
        {
            string nom = Normaliser(nomAffiche ?? "");
            if (nom.Length == 0) return null;
            foreach (var p in programmes)
            {
                if (!nom.StartsWith(p.Key, StringComparison.Ordinal)) continue;
                if (nom.Length == p.Key.Length) return p.Value;
                char suivant = nom[p.Key.Length];
                if (!((suivant >= 'a' && suivant <= 'z') || (suivant >= '0' && suivant <= '9'))) return p.Value;
            }
            return null;
        }

        /// <summary>Minuscules, sans accents, espaces réduits (même règle que le site d'Ergalo).</summary>
        public static string Normaliser(string texte)
        {
            string d = texte.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            bool espace = false;
            foreach (char ch in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsWhiteSpace(ch)) { espace = true; continue; }
                if (espace && sb.Length > 0) sb.Append(' ');
                espace = false;
                if (ch == 'œ' || ch == 'Œ') sb.Append("oe");
                else if (ch == 'æ' || ch == 'Æ') sb.Append("ae");
                else sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }
    }
}
