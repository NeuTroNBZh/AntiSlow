// =============================================================================
//  AntiSlowPlugin.cs
//  Plugin Counter-Strike 2 – Bloque le slow-walk (Shift) des joueurs ciblés.
//
//  Auteur  : NeuTroNBZh
//  Version : 1.1.0
//  Cadre   : CounterStrikeSharp (.NET 8)
//  Version standalone : aucune dépendance externe à CS2-SimpleAdminApi.dll
// =============================================================================

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;

namespace AntiSlowPlugin;

// =============================================================================
//  Modèle de données
// =============================================================================

/// <summary>
/// Contient les informations d'un joueur dont le slow-walk est bloqué.
/// </summary>
public class BlockedPlayerData
{
    /// <summary>Nom du joueur au moment du blocage.</summary>
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>
    /// Nombre de rounds restants avant déblocage automatique.
    /// -1 = blocage permanent (jusqu'à !unantislow explicite).
    /// </summary>
    public int RoundsRemaining { get; set; }

    /// <summary>Raison facultative affichée lors du blocage.</summary>
    public string Reason { get; set; } = string.Empty;
}

// =============================================================================
//  Plugin principal
// =============================================================================

/// <summary>
/// Plugin AntiSlow : empêche les joueurs ciblés d'utiliser le slow-walk (Shift).
/// Fonctionne en mode standalone ou intégré à CS2-SimpleAdmin.
/// Les messages chat sont entièrement localisables via lang/*.json.
/// </summary>
public class AntiSlowPlugin : BasePlugin
{
    // --- Métadonnées du plugin -----------------------------------------------

    public override string ModuleName        => "AntiSlow";
    public override string ModuleVersion     => "1.1.1";
    public override string ModuleAuthor      => "NeuTroNBZh";
    public override string ModuleDescription => "Bloque le slow-walk (Shift) des joueurs ciblés.";

    // --- Stockage des joueurs bloqués ----------------------------------------

    /// <summary>
    /// Dictionnaire des joueurs dont le slow-walk est actuellement bloqué.
    /// Clé : SteamID64 du joueur.
    /// </summary>
    private readonly Dictionary<ulong, BlockedPlayerData> _blockedPlayers = new();

    // =========================================================================
    //  CYCLE DE VIE DU PLUGIN
    // =========================================================================

    /// <summary>
    /// Chargement du plugin.
    /// Enregistre les commandes, le listener de tick et les hooks d'événements.
    /// </summary>
    public override void Load(bool hotReload)
    {
        Logger.LogInformation("[AntiSlow] Chargement du plugin AntiSlow v{Version} par NeuTroNBZh.", ModuleVersion);

        // --- Commandes chat / console ---
        // !antislow   → css_antislow
        // !unantislow → css_unantislow
        // !antislowlist → css_antislowlist
        AddCommand("css_antislow",     "Bloque le slow-walk d'un joueur.",              OnAntiSlowCommand);
        AddCommand("css_unantislow",   "Débloque le slow-walk d'un joueur.",            OnUnAntiSlowCommand);
        AddCommand("css_antislowlist", "Liste les joueurs dont le slow est bloqué.",    OnAntiSlowListCommand);

        // --- Listener de tick : suppression de l'input Speed en temps réel ---
        RegisterListener<Listeners.OnTick>(OnTick);

        // --- Hook fin de round : gestion du décompte des rounds ---------------
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);

        // --- Hook déconnexion : nettoyage du dictionnaire ---------------------
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);

        Logger.LogInformation("[AntiSlow] Plugin chargé avec succès.");
    }

    public override void Unload(bool hotReload)
    {
        Logger.LogInformation("[AntiSlow] Plugin déchargé.");
    }

    // =========================================================================
    //  LISTENER DE TICK : SUPPRESSION DU SLOW-WALK EN TEMPS RÉEL
    // =========================================================================

    /// <summary>
    /// Appelé à chaque tick serveur.
    /// Pour chaque joueur bloqué actuellement en jeu, supprime le flag PlayerButtons.Speed
    /// (correspondant à la touche Shift / slow-walk) s'il est actif.
    /// </summary>
    private void OnTick()
    {
        // Optimisation : ne rien faire si aucun joueur n'est bloqué
        if (_blockedPlayers.Count == 0)
            return;

        foreach (var player in Utilities.GetPlayers())
        {
            // Filtres de sécurité
            if (!player.IsValid || player.IsBot || !player.PawnIsAlive)
                continue;

            // Vérifier si ce joueur est dans la liste des bloqués
            if (!_blockedPlayers.ContainsKey(player.SteamID))
                continue;

            // Accès au pawn du joueur
            var pawn = player.PlayerPawn?.Value;
            if (pawn == null)
                continue;

            // Récupération des services de mouvement (cast nécessaire depuis la classe de base)
            var movementServices = pawn.MovementServices as CCSPlayer_MovementServices;
            if (movementServices == null)
                continue;

            // Si le flag Speed (Shift) est actif, le retirer des inputs
            if ((movementServices.Buttons.ButtonStates[0] & (ulong)PlayerButtons.Speed) != 0)
            {
                movementServices.Buttons.ButtonStates[0] &= ~(ulong)PlayerButtons.Speed;
            }
        }
    }

    // =========================================================================
    //  HOOK FIN DE ROUND : DÉCOMPTE DES ROUNDS RESTANTS
    // =========================================================================

    /// <summary>
    /// Gère l'événement de fin de round.
    /// Décrémente le compteur de rounds de chaque joueur bloqué avec un timer.
    /// Débloque automatiquement et annonce dans le chat global quand le compteur atteint 0.
    /// </summary>
    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        // Liste des SteamIDs à débloquer en fin de parcours (évite la modification en cours d'itération)
        var toUnblock = new List<(ulong SteamId, string PlayerName)>();

        foreach (var kvp in _blockedPlayers)
        {
            // -1 = permanent, ne pas décrémenter
            if (kvp.Value.RoundsRemaining == -1)
                continue;

            kvp.Value.RoundsRemaining--;

            if (kvp.Value.RoundsRemaining <= 0)
                toUnblock.Add((kvp.Key, kvp.Value.PlayerName));
        }

        // Traitement des déblocages automatiques
        foreach (var (steamId, playerName) in toUnblock)
        {
            _blockedPlayers.Remove(steamId);

            Server.PrintToChatAll(Localizer["antislow.chat.expired", playerName]);

            Logger.LogInformation(
                "[AntiSlow] Déblocage automatique de {PlayerName} – timer de rounds expiré.",
                playerName);
        }

        return HookResult.Continue;
    }

    // =========================================================================
    //  HOOK DÉCONNEXION : NETTOYAGE DU DICTIONNAIRE
    // =========================================================================

    /// <summary>
    /// Gère l'événement de déconnexion d'un joueur.
    /// Si le joueur est dans la liste des bloqués, il en est retiré silencieusement.
    /// Cela évite toute fuite mémoire et des accès à des entités invalides.
    /// </summary>
    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event.Userid;

        // Vérifications de sécurité
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        var steamId = player.SteamID;

        if (_blockedPlayers.Remove(steamId))
        {
            Logger.LogInformation(
                "[AntiSlow] Joueur {Name} (SteamID: {SteamId}) déconnecté – retiré de la liste des bloqués.",
                player.PlayerName, steamId);
        }

        return HookResult.Continue;
    }

    // =========================================================================
    //  COMMANDES
    // =========================================================================

    // -------------------------------------------------------------------------
    //  !antislow <nom> [rounds] [raison...]
    // -------------------------------------------------------------------------

    /// <summary>
    /// Commande !antislow : bloque le slow-walk d'un joueur.
    /// Recherche par nom partiel insensible à la casse.
    /// Si plusieurs joueurs correspondent, liste les correspondances.
    /// </summary>
    private void OnAntiSlowCommand(CCSPlayerController? caller, CommandInfo command)
    {
        // Vérification de permission (@css/kick)
        if (!CheckPermission(caller))
            return;

        // Vérification du nombre d'arguments
        if (command.ArgCount < 2)
        {
            caller?.PrintToChat(Localizer["antislow.usage.antislow"]);
            return;
        }

        var nameArg = command.GetArg(1);
        var matches = FindPlayersByName(nameArg);

        if (matches.Count == 0)
        {
            caller?.PrintToChat(Localizer["antislow.player.notfound", nameArg]);
            return;
        }

        if (matches.Count > 1)
        {
            caller?.PrintToChat(Localizer["antislow.player.ambiguous", nameArg]);
            foreach (var m in matches)
                caller?.PrintToChat(Localizer["antislow.player.ambiguous.entry", m.PlayerName]);
            caller?.PrintToChat(Localizer["antislow.player.bespecific"]);
            return;
        }

        var target = matches[0];

        // --- Parsing des arguments optionnels ---
        int    roundsRemaining = -1;       // -1 = permanent par défaut
        string reason          = string.Empty;

        if (command.ArgCount >= 3)
        {
            // Si l'argument 2 est un entier ≥ 0, c'est le nombre de rounds
            if (int.TryParse(command.GetArg(2), out int parsedRounds))
            {
                // 0 ou négatif → permanent
                roundsRemaining = parsedRounds > 0 ? parsedRounds : -1;

                // La raison commence à l'argument 3
                if (command.ArgCount >= 4)
                    reason = BuildReasonString(command, startIndex: 3);
            }
            else
            {
                // L'argument 2 n'est pas un nombre → toute la suite est la raison
                reason = BuildReasonString(command, startIndex: 2);
            }
        }

        ApplyBlock(caller, target, roundsRemaining, reason);
    }

    // -------------------------------------------------------------------------
    //  !unantislow <nom>
    // -------------------------------------------------------------------------

    /// <summary>
    /// Commande !unantislow : débloque manuellement le slow-walk d'un joueur bloqué.
    /// Recherche par nom partiel parmi les joueurs actuellement bloqués.
    /// </summary>
    private void OnUnAntiSlowCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!CheckPermission(caller))
            return;

        if (command.ArgCount < 2)
        {
            caller?.PrintToChat(Localizer["antislow.usage.unantislow"]);
            return;
        }

        var nameArg = command.GetArg(1);
        var matches = _blockedPlayers
            .Where(kvp => kvp.Value.PlayerName.Contains(nameArg, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            caller?.PrintToChat(Localizer["antislow.blocked.notfound", nameArg]);
            return;
        }

        if (matches.Count > 1)
        {
            caller?.PrintToChat(Localizer["antislow.blocked.ambiguous"]);
            foreach (var m in matches)
                caller?.PrintToChat(Localizer["antislow.player.ambiguous.entry", m.Value.PlayerName]);
            caller?.PrintToChat(Localizer["antislow.player.bespecific"]);
            return;
        }

        var (steamId, data) = matches[0];

        // Chercher le joueur en ligne correspondant (peut être null si déjà déconnecté)
        var target = Utilities.GetPlayers()
            .FirstOrDefault(p => p.IsValid && p.SteamID == steamId);

        RemoveBlock(caller, steamId, data.PlayerName, target);
    }

    // -------------------------------------------------------------------------
    //  !antislowlist
    // -------------------------------------------------------------------------

    /// <summary>
    /// Commande !antislowlist : affiche la liste des joueurs bloqués à l'appelant uniquement.
    /// Format : nom – rounds restants (ou "permanent") – raison facultative.
    /// </summary>
    private void OnAntiSlowListCommand(CCSPlayerController? caller, CommandInfo command)
    {
        if (!CheckPermission(caller))
            return;

        if (_blockedPlayers.Count == 0)
        {
            caller?.PrintToChat(Localizer["antislow.list.empty"]);
            return;
        }

        caller?.PrintToChat(Localizer["antislow.list.header", _blockedPlayers.Count]);

        foreach (var kvp in _blockedPlayers)
        {
            var data = kvp.Value;

            var roundsText = data.RoundsRemaining == -1
                ? Localizer["antislow.list.rounds.permanent"].ToString()
                : Localizer["antislow.list.rounds.count", data.RoundsRemaining].ToString();

            var reasonSuffix = string.IsNullOrEmpty(data.Reason)
                ? string.Empty
                : Localizer["antislow.suffix.reason", data.Reason].ToString();

            caller?.PrintToChat(Localizer["antislow.list.entry", data.PlayerName, roundsText, reasonSuffix]);
        }
    }

    // =========================================================================
    //  MÉTHODES UTILITAIRES INTERNES
    // =========================================================================

    /// <summary>
    /// Vérifie que l'appelant possède la permission @css/kick.
    /// La console (caller null) dispose toujours de tous les droits.
    /// Affiche un message d'erreur privé si l'accès est refusé.
    /// </summary>
    /// <returns>true si autorisé, false sinon.</returns>
    private bool CheckPermission(CCSPlayerController? caller)
    {
        // La console a toujours accès
        if (caller == null)
            return true;

        if (!AdminManager.PlayerHasPermissions(caller, "@css/kick"))
        {
            caller.PrintToChat(Localizer["antislow.noperm"]);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Recherche tous les joueurs en ligne (non-bots) dont le nom contient
    /// le fragment donné, sans tenir compte de la casse.
    /// </summary>
    /// <param name="nameFragment">Fragment de nom à rechercher.</param>
    /// <returns>Liste des joueurs correspondants.</returns>
    private static List<CCSPlayerController> FindPlayersByName(string nameFragment)
    {
        return Utilities.GetPlayers()
            .Where(p =>
                p.IsValid &&
                !p.IsBot &&
                p.PlayerName.Contains(nameFragment, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Concatène les arguments de la commande à partir d'un index donné
    /// pour former la chaîne de raison.
    /// </summary>
    /// <param name="command">L'objet CommandInfo.</param>
    /// <param name="startIndex">Index (base 0 incluant le nom de commande) depuis lequel lire.</param>
    /// <returns>La raison sous forme de chaîne, ou string.Empty si aucun arg.</returns>
    private static string BuildReasonString(CommandInfo command, int startIndex)
    {
        if (startIndex >= command.ArgCount)
            return string.Empty;

        var parts = new List<string>();
        for (int i = startIndex; i < command.ArgCount; i++)
            parts.Add(command.GetArg(i));

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Applique le blocage du slow-walk à un joueur.
    /// Met à jour le dictionnaire et annonce l'action dans le chat global.
    /// </summary>
    /// <param name="admin">Joueur administrateur ayant exécuté la commande (null = console).</param>
    /// <param name="target">Joueur cible du blocage.</param>
    /// <param name="roundsRemaining">
    ///     Nombre de rounds de blocage. -1 pour permanent.
    /// </param>
    /// <param name="reason">Raison facultative affichée dans l'annonce publique.</param>
    private void ApplyBlock(
        CCSPlayerController? admin,
        CCSPlayerController  target,
        int                  roundsRemaining,
        string               reason)
    {
        if (!target.IsValid)
        {
            admin?.PrintToChat(Localizer["antislow.target.invalid"]);
            return;
        }

        var adminName = admin?.PlayerName ?? "Console";

        _blockedPlayers[target.SteamID] = new BlockedPlayerData
        {
            PlayerName      = target.PlayerName,
            RoundsRemaining = roundsRemaining,
            Reason          = reason
        };

        var roundsSuffix = roundsRemaining == -1
            ? string.Empty
            : Localizer["antislow.suffix.rounds", roundsRemaining].ToString();

        var reasonSuffix = string.IsNullOrEmpty(reason)
            ? string.Empty
            : Localizer["antislow.suffix.reason", reason].ToString();

        Server.PrintToChatAll(
            Localizer["antislow.chat.blocked", adminName, target.PlayerName, roundsSuffix, reasonSuffix]);

        Logger.LogInformation(
            "[AntiSlow] {Admin} a bloqué le slow de {Target} (rounds: {Rounds}, raison: \"{Reason}\").",
            adminName,
            target.PlayerName,
            roundsRemaining == -1 ? "permanent" : roundsRemaining.ToString(),
            reason);
    }

    /// <summary>
    /// Retire le blocage du slow-walk d'un joueur et annonce le déblocage dans le chat global.
    /// </summary>
    /// <param name="admin">Joueur administrateur ayant exécuté la commande (null = console).</param>
    /// <param name="steamId">SteamID64 du joueur à débloquer.</param>
    /// <param name="playerName">Nom du joueur (utilisé si le joueur est hors ligne).</param>
    /// <param name="target">
    ///     Contrôleur du joueur si en ligne, null s'il est déconnecté.
    /// </param>
    private void RemoveBlock(
        CCSPlayerController? admin,
        ulong                steamId,
        string               playerName,
        CCSPlayerController? target)
    {
        if (!_blockedPlayers.ContainsKey(steamId))
        {
            admin?.PrintToChat(Localizer["antislow.notblocked", playerName]);
            return;
        }

        _blockedPlayers.Remove(steamId);

        var adminName  = admin?.PlayerName ?? "Console";
        var targetName = (target != null && target.IsValid) ? target.PlayerName : playerName;

        Server.PrintToChatAll(Localizer["antislow.chat.unblocked", adminName, targetName]);

        Logger.LogInformation(
            "[AntiSlow] {Admin} a débloqué le slow de {Target}.",
            adminName, targetName);
    }
}
