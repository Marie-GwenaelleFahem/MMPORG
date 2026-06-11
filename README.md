# MMPong — Massive Multiplayer Pong

Réinvention multijoueur du Pong classique. Les joueurs rejoignent une partie en réseau local et contrôlent **collectivement** une palette : plus de joueurs appuient simultanément sur une direction, plus la palette accélère.

## Gameplay

- Un joueur héberge la partie (Host) depuis le menu principal
- Les autres joueurs rejoignent en choisissant un côté : **Gauche** ou **Droite**
- Les inputs de tous les joueurs d'un même côté sont agrégés — la vitesse de déplacement est proportionnelle au nombre d'inputs actifs simultanément
- Un compte à rebours (3-2-1-GO) lance la manche ; la balle repart au centre après chaque point

### Contrôles

- **Palette Gauche** : `Z` (haut) / `S` (bas)
- **Palette Droite** : `↑` (haut) / `↓` (bas)

---

## Lancer le projet

**Prérequis** : Unity 6 (6000.0.x recommandé), Git

```bash
git clone <repo-url>
```

Ouvrir `Assets/Demos/Pong/Pong.unity` dans l'éditeur Unity, puis **Play**.

- Pour héberger : cliquer **Host** dans le menu
- Pour rejoindre : cliquer **Join**, sélectionner le serveur découvert automatiquement, choisir son côté

---

## Architecture

Architecture **client-serveur UDP** en réseau local.

```
Assets/Demos/Pong/
├── Managers/
│   ├── GameNetworkManager.cs     # Singleton UI↔réseau ; stocke mode host/client et IP
│   ├── PongServerManager.cs      # Serveur autoritaire : simulation, broadcast état
│   └── PongClientManager.cs      # Client : envoi input, application état reçu
├── States/
│   └── PongMatchState.cs         # Conteneur sérialisable : pos balle, palettes, état
├── UDP/
│   └── UDPService.cs             # Couche UDP bas niveau (bind, send, receive, broadcast)
├── UI/
│   ├── MenuController.cs         # Navigation entre panneaux menu
│   ├── HostPanelUI.cs            # Sélection difficulté, démarrage hôte
│   ├── ClientPanelUI.cs          # Liste serveurs découverts, sélection côté
│   ├── PongSideAssignmentUI.cs   # Affiche l'assignation de côté en début de manche
│   ├── PongWinUI.cs              # Écran de fin de manche
│   └── WaitingUI.cs              # Attente de connexions
├── Input/
│   └── PongInput.cs              # Auto-généré depuis InputSystem (Player1 / Player2)
├── PongNetworkSession.cs         # Orchestrateur de session ; crée Server/ClientManager
├── PongBall.cs                   # Physique balle, détection collisions, états victoire
├── PongPaddle.cs                 # Déplacement palette, lecture input, multiplicateur vitesse
├── PongRoundFlow.cs              # Machine à états compte à rebours et assignation côtés
├── PongCountdownUI.cs            # Overlay "3-2-1-GO"
├── PongPaddleInput.cs            # Helper statique : lecture clavier → axe [-1, +1]
└── PongNetworkPorts.cs           # Constantes : GamePort=25000, DiscoveryPort=25001
```

### Flux réseau

**Ports** : `25000` (jeu), `25001` (découverte LAN)

**Découverte** : le serveur broadcast un beacon sur `25001` ; les clients écoutent et affichent automatiquement les parties disponibles.

**Protocole texte** (messages délimités par `\n`) :

| Direction | Format | Signification |
|-----------|--------|---------------|
| Client → Serveur | `J\|L` / `J\|R` | Demande de rejoindre (côté gauche/droite) |
| Serveur → Client | `A\|L\|0.5\|2` | Assignation : côté, part de vitesse, nb joueurs |
| Client → Serveur | `I\|0.5` | Input : axe de déplacement [-1, +1] |
| Serveur → Clients | `S\|bx\|by\|pl\|pr\|state` | État du jeu : pos balle, palettes Y, état balle |
| Serveur → Clients | `R` | Reset de manche |
| Serveur (broadcast) | `B\|PongHost\|25000` | Beacon de découverte |

**Agrégation des inputs** : chaque joueur contribue `1/nb_joueurs_côté` à la vitesse de sa palette. Le serveur est autoritaire — il simule la balle et broadcast l'état toutes les 20 ms (~50 Hz).

### Patterns clés

- **Singleton persistant** : `GameNetworkManager` et `PongNetworkSession` survivent au chargement de scène (`DontDestroyOnLoad`)
- **Séparation simulation/affichage** : seul le serveur simule la physique ; les clients appliquent l'état reçu
- **Round flow** : `PongRoundFlow` gère le compte à rebours puis déverrouille le gameplay

---

## Stack

- **Engine** : Unity 6
- **Langage** : C#
- **Réseau** : UDP custom (pas de Netcode for GameObjects, pas de Mirror)
- **Input** : Unity Input System

---

## Équipe

Groupe 5 — Waris, Benjamin, Marie-Gwenaëlle
