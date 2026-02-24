namespace Turandot;
static class Extensions
{
	static readonly Random random = new();
	extension<T>(IList<T> list)
	{
		public void Shuffle()
		{
			for(var i = list.Count; i-- > 0;)
			{
				var index = random.Next(0, i);
				(list[i], list[index]) = (list[index], list[i]);
			}
		}
	}
}
