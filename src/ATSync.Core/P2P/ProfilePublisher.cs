using ATSync.Core.Models;
using ATSync.Core.Profiles;

namespace ATSync.Core.P2P;

/// <summary>Coordina la publicación de un perfil ATSync: lo serializa, calcula CID, comparte via ProfileTransfer.</summary>
public sealed class ProfilePublisher
{
    private readonly PeerIdentity _identity;
    private readonly ProfileTransfer _transfer;
    private readonly ProfileRepository _repo;
    private readonly string _stagingDir;

    public ProfilePublisher(PeerIdentity identity, ProfileTransfer transfer, ProfileRepository repo, string stagingDir)
    {
        _identity = identity;
        _transfer = transfer;
        _repo = repo;
        _stagingDir = stagingDir;
    }

    /// <summary>Publica un perfil: lo escribe localmente y devuelve el URI compartible.</summary>
    public ProfileUri Publish(AtsyncProfile profile)
    {
        var cid = CidBuilder.ComputeCid(System.Text.Encoding.UTF8.GetBytes(ProfileSerializer.Serialize(profile)));
        // Asegurar que el perfil tenga el PeerId del publicador
        profile.Id = _identity.PeerId;
        _repo.Save(profile);
        return new ProfileUri { Scheme = "profile", Cid = cid };
    }
}