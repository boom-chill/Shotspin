using Unity.Netcode;
using System;

[Serializable]
public struct NetworkBullet : INetworkSerializable, System.IEquatable<NetworkBullet>
{
    public BulletType bulletType;

    public NetworkBullet(BulletType type)
    {
        bulletType = type;
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
        return (int)bulletType;
    }

    public static bool operator ==(NetworkBullet left, NetworkBullet right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(NetworkBullet left, NetworkBullet right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return bulletType.ToString();
    }
}