using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace GroundControl.Persistence.MongoDb.Conventions;

/// <summary>
/// Registers global BSON conventions for GroundControl.
/// </summary>
public static class MongoConventions
{
    private const string ConventionPackName = "GroundControl";
    private static int _isRegistered;

    /// <summary>
    /// Registers MongoDB BSON conventions and serializers.
    /// </summary>
    public static void Register()
    {
        if (Interlocked.Exchange(ref _isRegistered, 1) == 1)
        {
            return;
        }

        var conventionPack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new EnumRepresentationConvention(BsonType.String),
            new IgnoreExtraElementsConvention(ignoreExtraElements: true)
        };

        ConventionRegistry.Register(ConventionPackName, conventionPack, _ => true);
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
    }
}