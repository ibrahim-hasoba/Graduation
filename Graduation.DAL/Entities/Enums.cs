namespace Graduation.DAL.Entities
{
    public enum OrderStatus
    {
        Pending = 1,
        Confirmed = 2,
        Processing = 3,
        Shipped = 4,
        Delivered = 5,
        Cancelled = 6,
        Returned = 7
    }

    public enum PaymentMethod
    {
        CashOnDelivery = 1,
        CreditCard = 2,
        VodafoneCash = 3,
        InstaPay = 4,
        BankTransfer = 5
    }

    public enum PaymentStatus
    {
        Pending = 1,
        Paid = 2,
        Failed = 3,
        Refunded = 4
    }

    public enum EgyptianGovernorate
    {
        Cairo = 1,
        Giza = 2,
        Alexandria = 3,
        Dakahlia = 4,
        RedSea = 5,
        Beheira = 6,
        Fayoum = 7,
        Gharbia = 8,
        Ismailia = 9,
        Menofia = 10,
        Minya = 11,
        Qaliubiya = 12,
        NewValley = 13,
        Suez = 14,
        Aswan = 15,
        Assiut = 16,
        BeniSuef = 17,
        PortSaid = 18,
        Damietta = 19,
        Sharkia = 20,
        SouthSinai = 21,
        KafrElSheikh = 22,
        Matrouh = 23,
        Luxor = 24,
        Qena = 25,
        NorthSinai = 26,
        Sohag = 27
    }
}