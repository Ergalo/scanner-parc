// La marque à usage unique : elle est dans le nom du fichier que le site propose au téléchargement
// (route /api/vos-outils/[jeton]/scanner), ergalo-releve-AAAAMMJJ-<identifiant>.exe, et plus collée au bout du programme :
// le contenu du fichier reste ainsi celui de la version publiée, signable et reconnu par son empreinte. Le programme lit
// son propre nom, sans Internet, refuse de travailler sans marque ou après sa date, et recopie l'identifiant dans le
// fichier produit. C'est le site qui décide au dépôt (autre questionnaire, déjà déposé, expiré) : renommer le fichier
// ou changer la date ne sert à rien.
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Ergalo.Releve
{
    internal sealed class Marque
    {
        /// <summary>Le nom donné par le site : la date est le dernier jour de validité, l'identifiant fait 43 caractères.
        /// Un suffixe ajouté par le navigateur (« (1) ») ne gêne pas.</summary>
        private static readonly Regex Motif = new Regex(@"ergalo-releve-(\d{8})-([A-Za-z0-9_-]{43})(?![A-Za-z0-9_-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public string Json;           // recopié dans le fichier produit, au format ergalo-marque version 1
        public string Id;
        public DateTimeOffset ExpireLe;

        /// <summary>La marque portée par ce nom de fichier, ou null s'il n'en porte pas (fichier renommé, date impossible).</summary>
        public static Marque LireNom(string nomFichier)
        {
            if (string.IsNullOrEmpty(nomFichier)) return null;
            Match m = Motif.Match(nomFichier);
            if (!m.Success) return null;
            DateTime jour;
            if (!DateTime.TryParseExact(m.Groups[1].Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out jour)) return null;
            string id = m.Groups[2].Value;
            string date = jour.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return new Marque
            {
                Json = "{\"format\":\"ergalo-marque\",\"version\":1,\"id\":\"" + id + "\",\"expire_le\":\"" + date + "\"}",
                Id = id,
                // Valable jusqu'à la fin du jour indiqué ; le site tient l'heure exacte.
                ExpireLe = new DateTimeOffset(jour.AddDays(1), TimeSpan.Zero),
            };
        }

        public bool Expiree(DateTime maintenantUtc)
        {
            return maintenantUtc >= ExpireLe.UtcDateTime;
        }
    }
}
