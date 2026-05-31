# FacturXValidator

Application web Blazor Server en .NET 10 pour deposer des factures PDF et realiser une validation indicative Factur-X / EN 16931.

## Demarrage local

```powershell
dotnet restore .\FacturXValidatorSaas\FacturXValidator.csproj
dotnet run --project .\FacturXValidatorSaas\FacturXValidator.csproj
```

Par defaut, la configuration pointe vers `/app/data/uploads` et `/app/data/schemas`, ce qui correspond au conteneur Docker. En local Windows, vous pouvez surcharger :

```powershell
$env:TemporaryFiles__UploadPath="data/uploads"
$env:FacturX__SchemasPath="data/schemas"
dotnet run --project .\FacturXValidatorSaas\FacturXValidator.csproj
```

## Demarrage Docker

```powershell
docker compose up --build
```

Le site sera expose sur `http://localhost:8080`.

## Configuration des uploads

Les principales options sont configurables dans `appsettings.json` ou via variables d'environnement :

```json
"Upload": {
  "MaxFileSizeMb": 20,
  "MaxFilesPerBatch": 10,
  "AllowedContentTypes": [ "application/pdf", "application/x-pdf" ]
}
```

Chaque fichier est valide cote serveur : extension `.pdf`, Content-Type autorise, taille maximale, nom serveur aleatoire et signature `%PDF-`. Le nom original n'est jamais utilise pour le stockage.

## Suppression automatique

`TemporaryFileCleanupService` supprime les fichiers du dossier temporaire configures depuis plus de 24 heures par defaut :

```json
"TemporaryFiles": {
  "UploadPath": "/app/data/uploads",
  "RetentionHours": 24,
  "CleanupIntervalMinutes": 60
}
```

Le service normalise le chemin configure et ne supprime jamais en dehors de ce dossier.

## Validation Factur-X

La validation actuelle couvre :

- ouverture PDF via la bibliotheque NuGet `PDFsharp` ;
- verification de la signature PDF ;
- detection indicative PDF/A-3 via metadonnees ;
- extraction d'un XML Factur-X depuis le contenu PDF brut ou des streams FlateDecode ;
- verification XML bien forme avec resolvers externes desactives ;
- detection UN/CEFACT CII ;
- extraction du profil, numero, date, vendeur, acheteur, montants et devise ;
- controle simple `TTC = HT + TVA` avec tolerance configurable ;
- rapport par fichier avec erreurs, avertissements et informations.

## Limites connues

La conformite Factur-X complete exige les XSD et regles Schematron officiels, ainsi qu'une validation PDF/A-3 specialisee. L'interface `ISchemaValidationService` et le dossier `/app/data/schemas` sont prevus pour cette extension.

Prochaine etape recommandee : integrer les schemas XSD/Schematron officiels Factur-X / EN 16931 dans `BasicSchemaValidationService` ou une implementation dediee.

## Google AdSense

`AdsPlaceholder.razor` utilise :

```json
"Ads": {
  "Enabled": false,
  "ClientId": "",
  "SlotId": ""
}
```

Quand `Ads:Enabled` vaut `false`, un emplacement discret est affiche. Quand `true`, le composant rend le script AdSense avec `ClientId` et `SlotId`, sans bloquer le site si la configuration est absente.
