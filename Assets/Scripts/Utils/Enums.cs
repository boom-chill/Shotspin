using Unity.Netcode;

public enum BulletType
{
    Empty,   // Trống
    Normal,  // Đạn thường (1 damage)
    Gold     // Đạn vàng (2 damage)
}

public enum ItemType
{
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
    CardPlay,
    CardExecution,
    Shop,
    GameOver
}

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

    // Nếu bạn muốn .Contains() hay so sánh bằng == tiện lợi hơn
    public override bool Equals(object obj)
    {
        return obj is PlayerNetworkData other && Equals(other);
    }

    public override int GetHashCode()
    {
        return clientId.GetHashCode() ^ playerId.GetHashCode() ^ isReady.GetHashCode();
    }
}

// [System.Serializable]
// public struct NetworkBullet : INetworkSerializable, System.IEquatable<NetworkBullet>
// {
//     public BulletType bulletType;

//     public NetworkBullet(BulletType type)
//     {
//         bulletType = type;
//     }

//     public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
//     {
//         serializer.SerializeValue(ref bulletType);
//     }

//     public bool Equals(NetworkBullet other)
//     {
//         return bulletType == other.bulletType;
//     }

//     public override bool Equals(object obj)
//     {
//         return obj is NetworkBullet other && Equals(other);
//     }

//     public override int GetHashCode()
//     {
//         return bulletType.GetHashCode();
//     }

//     public static implicit operator BulletType(NetworkBullet nb) => nb.bulletType;
//     public static implicit operator NetworkBullet(BulletType bt) => new NetworkBullet(bt);
// }