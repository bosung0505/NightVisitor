using UnityEngine;

public enum HitboxType { Normal, Critical }

public class Hitbox : MonoBehaviour
{
    [Tooltip("치명타 부위(머리, 심장)는 Critical, 일반 몸통은 Normal로 설정하세요.")]
    public HitboxType type = HitboxType.Normal;

    public void TakeDamage(int baseDamage, Vector3 hitPoint)
    {
        int finalDamage = baseDamage;
        
        // Critical(머리/가슴)을 맞았을 경우 데미지 증폭 (기본 1데미지가 2로 불어남)
        if (type == HitboxType.Critical)
        {
            finalDamage *= 2;
        }

        // hit된 콜라이더의 부모(혹은 조상)에 있는 여우 메인 제어 스크립트를 찾음
        RandomFoxAnimation fox = GetComponentInParent<RandomFoxAnimation>();
        if (fox != null)
        {
            // 여우 체력 스크립트로 증폭된 데미지 전송
            fox.TakeDamage(finalDamage, hitPoint);
        }
        else
        {
            DecoyFoxAI decoyFox = GetComponentInParent<DecoyFoxAI>();
            if (decoyFox != null)
            {
                decoyFox.TakeDamage(finalDamage, hitPoint);
            }
            else
            {
                MonsterAI monster = GetComponentInParent<MonsterAI>();
                if (monster != null)
                {
                    monster.TakeDamage(finalDamage, hitPoint);
                }
                else
                {
                    Debug.LogWarning("Hitbox: 부모에서 RandomFoxAnimation, DecoyFoxAI 혹은 MonsterAI 스크립트를 찾을 수 없습니다! 구조를 확인해주세요.");
                }
            }
        }
    }
}
