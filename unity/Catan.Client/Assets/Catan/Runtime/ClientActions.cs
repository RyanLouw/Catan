#nullable disable
using System;
using System.Linq;
namespace Catan.Client
{
    /// <summary>Maps gestures to intent; legality and costs are supplied by the authoritative host.</summary>
    public static class ClientActions
    {
        public static Intent Drop(HostResponse response, string actor, string piece, int id)
        {
            if (response == null || response.View == null || response.View.ActivePlayer != actor) return null;
            var phase = response.View.Phase; string kind;
            if (piece == "Road" && (phase == "SetupRoad" || phase == "Action") && Contains(response.LegalRoads,id)) kind = phase == "SetupRoad" ? "PlaceRoad" : "BuildRoad";
            else if (piece == "Settlement" && (phase == "SetupSettlement" || phase == "Action") && Contains(response.LegalSettlements,id)) kind = phase == "SetupSettlement" ? "PlaceSettlement" : "BuildSettlement";
            else if (piece == "City" && phase == "Action" && Contains(response.LegalCities,id)) kind = "BuildCity";
            else if (piece == "Robber" && phase == "Robber" && id != response.View.RobberHex && response.View.Hexes.Any(h => h.Id == id)) kind = "MoveRobber";
            else return null;
            return new Intent { Kind = kind, Player = actor, Target = id, RequestId = Guid.NewGuid().ToString("N") };
        }
        public static bool Contains(int[] ids, int id) => ids != null && Array.IndexOf(ids,id) >= 0;
        public static double Remaining(HostResponse response, double elapsedSeconds)
        {
            if (response?.View?.DeadlineMilliseconds == null) return 0;
            long deadline=response.View.DeadlineMilliseconds.Value;
            if((response.View.Phase=="Robber"||response.View.Phase=="Victim")&&response.View.ActionDeadlineMilliseconds.HasValue)deadline=Math.Min(deadline,response.View.ActionDeadlineMilliseconds.Value);
            return Math.Max(0, (deadline-response.ServerMilliseconds)/1000.0 - Math.Max(0,elapsedSeconds));
        }
        public static string Instruction(string phase)
        {
            switch (phase)
            {
                case "Handover": return "Take a moment to hand over — start when you are ready";
                case "SetupSettlement": return "Drag a settlement onto a glowing intersection";
                case "SetupRoad": return "Drag a road beside your new settlement";
                case "AwaitRoll": return "Roll the dice to begin your turn";
                case "Discard": return "Choose the resources you must discard";
                case "Robber": return "Drag the robber onto another terrain tile";
                case "Victim": return "Choose a player to steal from";
                case "Action": return "Build, play cards or trade — then end your turn";
                case "Finished": return "Game complete";
                default: return "Waiting for the next player";
            }
        }
    }
}
