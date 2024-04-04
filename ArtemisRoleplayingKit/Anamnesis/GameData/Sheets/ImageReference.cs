// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.GameData.Sheets;


public class ImageReference
{
	public readonly uint ImageId;

	public ImageReference(uint imageId)
	{
		this.ImageId = imageId;
	}

	public ImageReference(ushort imageId)
	{
		this.ImageId = imageId;
	}

	public ImageReference(int imageId)
	{
		this.ImageId = (uint)imageId;
	}
}
