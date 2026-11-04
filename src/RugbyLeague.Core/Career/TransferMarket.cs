using RugbyLeague.Core.Model;

namespace RugbyLeague.Core.Careers;

public sealed class TransferListing
{
    public Guid PlayerId { get; set; }
    public Guid ClubId { get; set; }
    public int AskingPrice { get; set; }
    public int Season { get; set; }
}

public enum TransferStatus
{
    Pending,
    Accepted,
    Rejected,
    Expired
}

/// <summary>A bid from another club for one of the player's own listed players.</summary>
public sealed class TransferOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public Guid FromClubId { get; set; }
    public Guid ToClubId { get; set; }
    public string ToClubName { get; set; } = "";
    public int Fee { get; set; }
    public TransferStatus Status { get; set; } = TransferStatus.Pending;
    public int Season { get; set; }
    public int Round { get; set; }
}

public sealed class TransferMarket
{
    public List<TransferListing> Listings { get; set; } = new();
    public List<TransferOffer> Offers { get; set; } = new();
    public List<Player> FreeAgents { get; set; } = new();
    public int WindowClosesRound { get; set; } = 8;

    public IEnumerable<TransferOffer> PendingOffers => Offers.Where(o => o.Status == TransferStatus.Pending);

    public TransferListing? ListingFor(Guid playerId) => Listings.FirstOrDefault(l => l.PlayerId == playerId);
}
