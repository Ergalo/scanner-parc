// Le fichier produit, au format convenu avec l'étape « Listing de votre environnement digital » (ergalo-scanner, version 1) :
// JSON lisible, UTF-8, seulement les éléments laissés cochés, jamais d'adresse de page, de titre, de chemin ni de nom
// d'utilisateur. 2 000 éléments et 2 Mo au plus.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Ergalo.Releve
{
    internal static class Export
    {
        public const int MaxElements = 2000;

        public static string NomFichier(DateTime maintenantLocal)
        {
            return "ergalo-outils-" + maintenantLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".json";
        }

        public static string Json(Marque marque, IEnumerable<Element> elements, DateTimeOffset creeLe)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"format\": \"ergalo-scanner\",\n");
            sb.Append("  \"version\": 1,\n");
            sb.Append("  \"marque\": ").Append(marque.Json).Append(",\n");
            sb.Append("  \"cree_le\": ").Append(Texte(Date(creeLe))).Append(",\n");
            sb.Append("  \"elements\": [");
            bool premier = true;
            foreach (Element e in elements.Where(x => x.Coche).Take(MaxElements))
            {
                sb.Append(premier ? "\n" : ",\n");
                premier = false;
                sb.Append("    ").Append(Ligne(e));
            }
            sb.Append(premier ? "]\n" : "\n  ]\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        private static string Ligne(Element e)
        {
            var champs = new List<string>();
            switch (e.Source)
            {
                case Source.Programme:
                case Source.Demarrage:
                    champs.Add(Champ("source", e.Source == Source.Programme ? "programme" : "demarrage"));
                    champs.Add(Champ("nom", e.Nom));
                    if (e.Executable != null) champs.Add(Champ("programme", e.Executable));
                    break;
                case Source.Extension:
                    champs.Add(Champ("source", "extension"));
                    champs.Add(Champ("nom", e.Nom));
                    champs.Add(Champ("navigateur", e.Navigateur));
                    break;
                case Source.Navigateur:
                    champs.Add(Champ("source", "navigateur"));
                    champs.Add(Champ("domaine", e.Domaine));
                    champs.Add("\"visites_90j\": " + e.Visites.ToString(CultureInfo.InvariantCulture));
                    champs.Add("\"navigateurs\": [" + string.Join(", ", e.NavigateursVus.Select(Texte)) + "]");
                    break;
            }
            if (e.Outil != null) champs.Add(Champ("outil", e.Outil.Id));
            return "{ " + string.Join(", ", champs) + " }";
        }

        public static string Date(DateTimeOffset d)
        {
            return d.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + d.ToString("zzz", CultureInfo.InvariantCulture);
        }

        private static string Champ(string cle, string valeur)
        {
            return Texte(cle) + ": " + Texte(valeur);
        }

        public static string Texte(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
