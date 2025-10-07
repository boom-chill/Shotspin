using Unity.Netcode;
using System;

[Serializable]
public struct NetworkBullet : INetworkSerializable, IEquatable<NetworkBullet>
{
    public BulletType bulletType;

    public NetworkBullet(BulletType t)
    {
        bulletType = t;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref bulletType);
    }

    public bool Equals(NetworkBullet other)
    {
        return bulletType == other.bulletType;
    }

    public override bool Equals(object obj)
    {
        return obj is NetworkBullet other && Equals(other);
    }

    public override int GetHashCode()
    {
        return bulletType.GetHashCode();
    }

    // convenient casts
    public static implicit operator BulletType(NetworkBullet nb) => nb.bulletType;
    public static implicit operator NetworkBullet(BulletType bt) => new NetworkBullet(bt);

    public override string ToString()
    {
        return bulletType.ToString();
    }
}