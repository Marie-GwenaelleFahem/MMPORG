# 🏓 MMPong - Massive Multiplayer Pong

## Le Jeu

**MMPong** est une réinvention multijoueur du classique Pong. Un jeu en temps réel où les joueurs se connectent en réseau pour contrôler collectivement les palettes et affronter l'adversaire.

### 🎮 Gameplay

**Contrôle Collectif des Palettes** :
- Les joueurs se connectent et choisissent : **Palette Gauche** ou **Palette Droite**
- Plus il y a de joueurs qui appuient **simultanément** sur une direction, plus la palette se déplace **rapidement**
- La dynamique du jeu change en temps réel selon le nombre de joueurs actifs
- **4+ joueurs** en réseau pour une expérience vraiment multijoueur

### ⌨️ Contrôles

- **Palette Gauche** : `Z` (Haut) / `S` (Bas)
- **Palette Droite** : `↑ Haut` / `↓ Bas`

---

## 🚀 Quick Start

### Prérequis
- **Unity 3D** 2021 LTS ou supérieur
- **C#** et notions de TCP/UDP
- **Git**

### Installation

```bash
# Clonez le dépôt
git clone [repo-url]
cd MMPORG

# Ouvrez dans Unity 3D
# Assets > Open Scene > Demos > Pong > Pong.unity
```

### Lancer le Jeu

1. Ouvrez `Assets/Demos/Pong/Pong.unity` dans l'éditeur Unity
2. Appuyez sur **Play** pour tester le jeu
3. Utilisez les contrôles ci-dessus

---

## 🏗️ Architecture

Le projet utilise une architecture **client-serveur** avec synchronisation en temps réel via **TCP/UDP**.

### Démos Incluses
- **TCP Example** : `Assets/Demos/TCP/TCP.unity` - Communication TCP basique
- **UDP Example** : `Assets/Demos/UDP/UDP.unity` - Communication UDP basique

### Structure du Code

```
Assets/
├── Demos/
│   ├── Pong/           # Scène principale du jeu
│   ├── TCP/            # Exemple TCP
│   └── UDP/            # Exemple UDP
├── Settings/           # Configuration du projet
└── InputSystem_Actions # Système d'entrée
```

---

## 💻 Technologies

- **Engine** : Unity 3D
- **Langage** : C#
- **Réseau** : TCP/UDP (protocole personnalisé)
- **Versioning** : Git

---

## � Équipe

**Groupe 5**
- Waris
- Benjamin
- Marie-Gwenaëlle
