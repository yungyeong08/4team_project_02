using UnityEngine;

[CreateAssetMenu(fileName = "DebtConfig", menuName = "Fishing/Debt Config")]
public sealed class DebtConfig : ScriptableObject
{
    [Min(0)] public int InitialDebt = 30000;
}
