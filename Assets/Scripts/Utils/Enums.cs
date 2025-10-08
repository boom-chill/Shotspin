using Unity.Netcode;

// ====================== ENUMS ======================

public enum BulletType
{
    Empty,   // Trống
    Normal,  // Đạn thường (1 damage)
    Gold     // Đạn vàng (2 damage)
}

public enum ItemType
{
    None,          // Thêm None cho empty slots
    Camera,        // Xem bài của 1 người (1 shell)
    Magnifier,     // Xem toàn bộ ổ đạn (1 shell)  
    LockBarrel,    // Vô hiệu hóa 1 turn (2 shells)
    LightShield,   // Chặn 1 máu (2 shells)
    Coffee,        // Đổi bài đã đánh (3 shells)
    HeavyShield,   // Chặn 2 máu (3 shells)
    Cigarette,     // Hồi 1 HP (4 shells)
    HotGift        // Biến đạn thành vàng (4 shells)
}

public enum CardType
{
    RotateBarrelLeft,      // Xoay nòng sang trái  
    RotateBarrelRight,     // Xoay nòng sang phải
    RotateCylinderLeft,    // Xoay ổ đạn 1 nấc sang trái
    RotateCylinderRight,   // Xoay ổ đạn 1 nấc sang phải
    SelfShoot,             // Người bị nòng trỏ tự bắn
    PeekBullet,            // Xem ô đạn ở vị trí đầu nòng
    SkipNext,              // Bỏ lượt người tiếp theo
    AddGoldBullet,         // Thêm 1 viên đạn vàng
    AddBullet,             // Thêm 1 viên đạn thường
    ShuffleCylinder,       // Xáo lại ổ đạn
    DrawCards,             // Được bốc thêm 2 lá
    Counter                // Phản lại
}

public enum GamePhase
{
    Setup,
    RevolverReveal,
    CardPlay,
    CardExecution,
    Shop,
    DrawCards,
    GameOver
}

// ====================== NETWORK STRUCTS ======================

[System.Serializable]
public struct PlayerNetworkData : INetworkSerializable, System.IEquatable<PlayerNetworkData>
{
    public ulong clientId;
    public int playerId;
    public bool isReady;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref playerId);
        serializer.SerializeValue(ref isReady);
    }

    public bool Equals(PlayerNetworkData other)
    {
        return clientId == other.clientId
            && playerId == other.playerId
            && isReady == other.isReady;
    }

    public override bool Equals(object obj)
    {
        return obj is PlayerNetworkData other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + clientId.GetHashCode();
            hash = hash * 31 + playerId.GetHashCode();
            hash = hash * 31 + isReady.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(PlayerNetworkData left, PlayerNetworkData right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(PlayerNetworkData left, PlayerNetworkData right)
    {
        return !left.Equals(right);
    }
}

// ====================== NETWORK CARD DATA ======================
[System.Serializable]
public struct NetworkCardData : INetworkSerializable, System.IEquatable<NetworkCardData>
{
    public CardType cardType;
    public int cardId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref cardType);
        serializer.SerializeValue(ref cardId);
    }

    public bool Equals(NetworkCardData other)
    {
        return cardType == other.cardType && cardId == other.cardId;
    }

    public override bool Equals(object obj)
    {
        return obj is NetworkCardData other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + cardType.GetHashCode();
            hash = hash * 31 + cardId.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(NetworkCardData left, NetworkCardData right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(NetworkCardData left, NetworkCardData right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"{cardType} (ID: {cardId})";
    }
}

// ====================== NETWORK ITEM DATA ======================
[System.Serializable]
public struct NetworkItemData : INetworkSerializable, System.IEquatable<NetworkItemData>
{
    public ItemType itemType;
    public int shellCost;
    public int tier;
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref itemType);
        serializer.SerializeValue(ref shellCost);
        serializer.SerializeValue(ref tier);
    }

    public bool Equals(NetworkItemData other)
    {
        return itemType == other.itemType 
            && shellCost == other.shellCost 
            && tier == other.tier;
    }

    public override bool Equals(object obj)
    {
        return obj is NetworkItemData other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + itemType.GetHashCode();
            hash = hash * 31 + shellCost.GetHashCode();
            hash = hash * 31 + tier.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(NetworkItemData left, NetworkItemData right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(NetworkItemData left, NetworkItemData right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"{itemType} - Tier {tier} ({shellCost} shells)";
    }
}