using UnityEngine;

namespace ArmOrb
{
    public sealed class WeaponView : MonoBehaviour
    {
        public SpriteRenderer artwork;
        public SpriteRenderer spearShaft;
        public SpriteRenderer spearHead;
        readonly WeaponPartPose[] parts=new WeaponPartPose[2];
        public void Draw(CombatantState orb,float radius,Vector2 position,float angle)
        {
            transform.position=position; transform.rotation=Quaternion.Euler(0,0,angle); transform.localScale=Vector3.one;
            int count=WeaponGeometry.GetParts(orb.Weapon,parts);
            for(int i=0;i<count;i++)
            {
                var renderer=i==0?artwork:spearHead; var part=parts[i];
                renderer.sprite=part.Mask.sprite;
                renderer.transform.localPosition=part.Offset*radius;
                if(part.Tiled)
                {
                    renderer.drawMode=SpriteDrawMode.Tiled;
                    float scale=part.Size.y*radius/renderer.sprite.bounds.size.y;
                    renderer.transform.localScale=Vector3.one*scale;
                    renderer.size=new Vector2(part.Size.x*radius/scale,renderer.sprite.bounds.size.y);
                }
                else
                {
                    renderer.drawMode=SpriteDrawMode.Simple;
                    renderer.transform.localScale=new Vector3(part.Size.x*radius/renderer.sprite.bounds.size.x,
                        part.Size.y*radius/renderer.sprite.bounds.size.y,1);
                }
            }
        }
    }
}
