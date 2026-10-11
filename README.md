# Cities Areas

**Cities Areas** adds independent municipal areas to *Cities: Skylines II*. Draw and edit municipal boundaries alongside the game's districts, then view the population, area and density associated with each municipality. Municipal areas can overlap districts and buildings without changing district membership.

The mod includes configurable interface language and area units, plus a configurable keyboard shortcut (default: **Ctrl+M**).

## Features

- Create and edit municipal boundaries independently of districts.
- View municipal population, area and population density.
- See which districts overlap a municipality and inspect their local population.
- Choose the interface language and area units in the mod settings.
- Open the Municipal Areas tool with **Ctrl+M** by default.

## Build

The C# project uses the Cities: Skylines II modding toolchain. Set the `CSII_TOOLPATH` environment variable to the folder containing `Mod.props` and `Mod.targets`, then run:

```powershell
dotnet build Communes.csproj
```

To build the UI bundle:

```powershell
cd Communes
npm ci
npm run build
```

## Languages

The mod interface includes English, French, Spanish, Italian, Simplified Chinese, German, Japanese, Polish, Portuguese, Korean, Russian and Traditional Chinese.

---

# Cities Areas (français)

**Cities Areas** ajoute des communes indépendantes dans *Cities: Skylines II*. Dessinez et modifiez leurs limites en parallèle des quartiers du jeu, puis consultez la population, la superficie et la densité associées à chaque commune. Les communes peuvent chevaucher les quartiers et les bâtiments sans modifier l'appartenance aux quartiers.

Le mod propose le choix de la langue de l'interface et des unités de superficie, ainsi qu'un raccourci clavier configurable (**Ctrl+M** par défaut).

## Fonctionnalités

- Créer et modifier des communes indépendamment des quartiers.
- Consulter la population, la superficie et la densité de chaque commune.
- Voir les quartiers qui chevauchent une commune et leur population locale.
- Choisir la langue de l'interface et les unités de superficie dans les paramètres.
- Ouvrir l'outil Zones communales avec **Ctrl+M** par défaut.

## Compilation

Le projet C# utilise l'outil de modding de Cities: Skylines II. Définissez la variable d'environnement `CSII_TOOLPATH` vers le dossier contenant `Mod.props` et `Mod.targets`, puis lancez :

```powershell
dotnet build Communes.csproj
```

Pour compiler l'interface :

```powershell
cd Communes
npm ci
npm run build
```

## Langues

L'interface du mod comprend l'anglais, le français, l'espagnol, l'italien, le chinois simplifié, l'allemand, le japonais, le polonais, le portugais, le coréen, le russe et le chinois traditionnel.
