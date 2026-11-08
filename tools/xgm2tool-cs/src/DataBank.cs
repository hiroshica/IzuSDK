using System.Text;
namespace Isdk.Xgm2Tool;


public class DataBank
{
    public int id;
    public byte[] data;

    public DataBank(VGMCommand command)
    {
        if (!command.isDataBlock())
            throw new ArgumentException(J.Format("Incorrect sample data declaration at %6X !",
                    (int)(command.getOriginOffset())));

        // id
        id = command.getDataBankId();
        // allocate data
        data = new byte[command.getDataBlockLen()];

        // copy bank data
        Array.Copy(command.data, 7, data, 0, data.Length);
    }

    public byte getSample(int offset)
    {
        return data[offset];
    }
}
