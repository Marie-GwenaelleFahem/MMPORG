# Phase 2 & 3 Tickets - MMPong Groupe 5

Ce fichier documente les tickets qui doivent être créés pour les phases 2 et 3 du projet.

## Phase 2 : Multijoueur Massif

### #8 - Sélection de Palette (Gauche/Droite)
- Priorité: 🟠 HAUTE
- Effort: 3 points
- Type: Feature

Ajouter une UI pour que les joueurs choisissent quelle palette contrôler (Gauche ou Droite).

**Critères d'acceptation:**
- Menu avant la partie
- Le joueur choisit : Palette Gauche ou Droite
- Choice envoyée au serveur
- Serveur confirme et affecte le joueur

**UI Needed:**
- 2 boutons : "Gauche" / "Droite"
- Indicateur : "Vous contrôlez : Palette Gauche"

---

### #9 - Contrôle Collectif des Palettes
- Priorité: 🟠 HAUTE
- Effort: 5 points
- Type: Feature

Implémenter la logique de contrôle collectif : plus de joueurs = plus vite.

**Critères d'acceptation:**
- Serveur combine inputs de plusieurs joueurs pour 1 palette
- Vitesse = (nombre de joueurs appuyant UP) * (speedBase)
- Teste avec 4 joueurs : palette monte plus vite avec plus d'inputs
- Maximum 4 joueurs par palette

**Détail Technique:**
- Tracker : playersHoldingUp[PaletteID] count
- Speed = speedBase * count (ex: speedBase = 5 units/s)

---

### #10 - Gestion Multijoueur - Entrée/Sortie
- Priorité: 🟠 HAUTE
- Effort: 4 points
- Type: Feature

Gérer les joueurs qui se connectent et se déconnectent dynamiquement.

**Critères d'acceptation:**
- Nouvelles connexions acceptées pendant la partie
- Déconnexions gérées sans crash
- Palette vide si tous les joueurs du side partent
- Nombre de joueurs affiché en temps réel

---

## Phase 3 : Optimisation & Polish

### #11 - Interface de Connexion Robuste
- Priorité: 🟡 MOYENNE
- Effort: 3 points
- Type: Feature

Créer une UI professionnelle pour connexion/reconnexion avec gestion d'erreurs.

**Critères d'acceptation:**
- Screen principal : champ IP + bouton "Jouer"
- Indicateur de connexion (vert/rouge/jaune)
- Messages d'erreur clairs : "Serveur introuvable", "Timeout", etc.
- Bouton "Quitter" toujours disponible

---

### #12 - Réduction Bande Passante
- Priorité: 🟡 MOYENNE
- Effort: 4 points
- Type: Technical

Optimiser le protocole pour réduire la consommation réseau.

**Critères d'acceptation:**
- Packets compressés (delta encoding)
- Envoyer que les changements d'état
- Fréquence adaptative (slow si pas d'action)
- Mesure : < 50 kbps pour 4 joueurs

**Technique:**
- Delta encoding : envoyer (newPos - lastPos)
- Skip send si position identique

---

### #13 - Réduction Latence & Prédiction Client
- Priorité: 🟡 MOYENNE
- Effort: 5 points
- Type: Technical

Implémenter prédiction client et compensation de latence.

**Critères d'acceptation:**
- Client prédit position locale avant ACK serveur
- Interpolation smooth entre états
- Pas de "jitter" ou "pop" visible
- Input lag < 50ms perceptible

**Technique:**
- Client-side prediction de la palette
- Interpolation LERP entre frames

---

### #14 - Protocole Personnalisé Documenté
- Priorité: 🟡 MOYENNE
- Effort: 2 points
- Type: Technical

Documenter le protocole UDP personnalisé en détail (format packets, states, messages).

**Critères d'acceptation:**
- Document : Format de chaque type de packet
- Format: [Header 1 byte] [Data N bytes]
- Exemple : 0x01 = PlayerInput, 0x02 = GameState
- Tous les packets documentés

---

### #15 - Graphismes & Feedback (Polish)
- Priorité: 🟡 MOYENNE
- Effort: 3 points
- Type: Feature

Ajouter du feedback visuel pour améliorer l'expérience (couleurs, animations, etc.).

**Critères d'acceptation:**
- Palettes changent de couleur selon le side (bleu/rouge)
- Animation paddle lisse
- Feedback impact balle (flash, son)
- Score animé

---

### #16 - Déploiement Internet (Bonus)
- Priorité: 🟢 BASSE
- Effort: 3 points
- Type: DevOps

Déployer un serveur public pour jouer sur Internet.

**Critères d'acceptation:**
- Serveur déployé (VPS/AWS/Azure)
- IP publique accessible
- Documentation "Comment jouer"
- Server peut gérer 8+ connexions simultanées

---

## Testing & QA

### #17 - Tests 2-joueurs Local
- Priorité: 🔴 CRITIQUE
- Effort: 2 points
- Type: Testing

Tester le jeu en 2-joueurs sur localhost.

**Test Cases:**
- [ ] Les 2 clients voient la même balle
- [ ] Collision détectée par les 2
- [ ] Score synchronisé
- [ ] Déconnexion propre

---

### #18 - Tests 4+ joueurs
- Priorité: 🔴 CRITIQUE
- Effort: 3 points
- Type: Testing

Tester avec 4+ joueurs (2 par palette) simultanés.

**Test Cases:**
- [ ] Serveur accepte 4+ connexions
- [ ] Palette monte plus vite quand 2 joueurs appuient
- [ ] État correct pour tous les clients
- [ ] Aucun crash après 5min de jeu

---

### #19 - Tests Réseau (Latence/Packet Loss)
- Priorité: 🟡 MOYENNE
- Effort: 2 points
- Type: Testing

Simuler conditions réseau dégradées (latence, packet loss).

**Test Cases:**
- [ ] Latence 100ms : jeu reste jouable
- [ ] Latence 500ms : dégradation perceptible mais OK
- [ ] 10% packet loss : jeu stable
- [ ] Récupération après timeout

---

## Documentation & Code Quality

### #20 - Architecture Documentation
- Priorité: 🟡 MOYENNE
- Effort: 2 points
- Type: Documentation

Documenter la structure architecture du code (diagrammes, explications).

**Contenu:**
- [ ] Diagramme Architecture (Client/Serveur)
- [ ] Flow de connexion
- [ ] State diagram
- [ ] Protocole messages

---

### #21 - Code Review & Refactoring
- Priorité: 🟡 MOYENNE
- Effort: 3 points
- Type: Technical

Nettoyer le code, ajouter commentaires, respecter les conventions.

**Acceptation Criteria:**
- [ ] Pas de code mort
- [ ] Noms variables clairs
- [ ] Commentaires sur logique complexe
- [ ] Consistent indentation/style

---

### #22 - Presentation Preparation
- Priorité: 🔴 CRITIQUE
- Effort: 2 points
- Type: Documentation

Préparer la présentation pour le 12 juin (slides + démo).

**Contenu:**
- [ ] Slides architecture
- [ ] Vidéo démo (4+ joueurs)
- [ ] Explications clés du code
- [ ] Points techniques clés expliqués
