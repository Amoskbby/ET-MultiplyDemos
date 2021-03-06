﻿using UnityEngine;
using System.Collections.Generic;

namespace ETModel
{
    public static class SekiaHelper
    {
        
        public static async ETVoid Login(string account, string password)
        {
            SekiaLoginComponent login = Game.Scene.GetComponent<FUIComponent>().Get(FUIType.SekiaLogin).GetComponent<SekiaLoginComponent>();

            Session sessionRealm = Game.Scene.GetComponent<NetOuterComponent>().Create(GlobalConfigComponent.Instance.GlobalProto.Address);
            A0002_Login_R2C messageRealm = (A0002_Login_R2C)await sessionRealm.Call(new A0002_Login_C2R() { Account = account, Password = password });
            sessionRealm.Dispose();
            login.Prompt.GObject.asTextField.text = "正在登录中...";

            //判断Realm服务器返回结果
            if (messageRealm.Error == ErrorCode.ERR_AccountOrPasswordError)
            {
                login.Prompt.GObject.asTextField.text = "登录失败,账号或密码错误";
                login.AccountInput.Get("Input").GObject.asTextInput.text = "";
                login.PasswordInput.Get("Input").GObject.asTextInput.text = "";
                login.isLogining = false;
                return;
            }
            //判断通过则登陆Realm成功

            //创建唯一Session
            Session sessionGate = Game.Scene.GetComponent<NetOuterComponent>().Create(messageRealm.GateAddress);
            if (SessionComponent.Instance == null)
            {
                //Log.Debug("创建唯一Session");
                Game.Scene.AddComponent<SessionComponent>().Session = sessionGate;
            }
            else
            {
                SessionComponent.Instance.Session = sessionGate;
            }
            
            A0003_LoginGate_G2C messageGate = (A0003_LoginGate_G2C)await sessionGate.Call(new A0003_LoginGate_C2G() { GateLoginKey = messageRealm.GateLoginKey });
            
            //判断登陆Gate服务器返回结果
            if (messageGate.Error == ErrorCode.ERR_ConnectGateKeyError)
            {
                login.Prompt.GObject.asTextField.text = "连接网关服务器超时";
                login.AccountInput.Get("Input").GObject.asTextInput.text = "";
                login.PasswordInput.Get("Input").GObject.asTextInput.text = "";
                sessionGate.Dispose();
                login.isLogining = false;
                return;
            }
            //判断通过则登陆Gate成功
            
            login.Prompt.GObject.asTextField.text = "";
            User user = ComponentFactory.Create<User, long>(messageGate.UserID);
            GamerComponent.Instance.MyUser = user;
            //Log.Debug("登陆成功");

            //获取角色信息判断应该进入哪个界面
            A0008_GetUserInfo_G2C messageUser = (A0008_GetUserInfo_G2C)await sessionGate.Call(new A0008_GetUserInfo_C2G());
            //消息中包含了3个CharacterInfo

            //正常的角色等级为正整数
            if (messageUser.Characters.Count == 3 &&
                messageUser.Characters[0].Level == 0 && 
                messageUser.Characters[1].Level == 0 && 
                messageUser.Characters[2].Level == 0)
            {
                //进入创建角色界面
                CreateCharacterFactory.Create(messageUser);
                CreateCharacterComponent creater = Game.Scene.GetComponent<FUIComponent>().Get(FUIType.CreateCharacter).GetComponent<CreateCharacterComponent>();
                creater.Seat = 1; //创建第一个空位的角色
            }
            else if(messageUser.Characters.Count == 3 &&
                (messageUser.Characters[0].Level > 0 ||
                messageUser.Characters[1].Level > 0 ||
                messageUser.Characters[2].Level > 0))
            {
                //进入角色选择界面
                SelectCharacterFactory.Create(messageUser);
            }

            Game.EventSystem.Run(EventIdType.SekiaLoginFinish);
        }

        public static async ETVoid Register(string account, string password)
        {
            Session session = Game.Scene.GetComponent<NetOuterComponent>().Create(GlobalConfigComponent.Instance.GlobalProto.Address);
            A0001_Register_R2C message = (A0001_Register_R2C)await session.Call(new A0001_Register_C2R() { Account = account, Password = password });
            session.Dispose();

            SekiaLoginComponent login = Game.Scene.GetComponent<FUIComponent>().Get(FUIType.SekiaLogin).GetComponent<SekiaLoginComponent>();
            login.isRegistering = false;

            if (message.Error == ErrorCode.ERR_AccountAlreadyRegisted)
            {
                login.Prompt.GObject.asTextField.text = "注册失败，账号已被注册";
                login.AccountInput.Get("Input").GObject.asTextInput.text = "";
                login.PasswordInput.Get("Input").GObject.asTextInput.text = "";
                return;
            }

            if (message.Error == ErrorCode.ERR_RepeatedAccountExist)
            {
                login.Prompt.GObject.asTextField.text = "注册失败，出现重复账号";
                login.AccountInput.Get("Input").GObject.asTextInput.text = "";
                login.PasswordInput.Get("Input").GObject.asTextInput.text = "";
                return;
            }
            
            login.Prompt.GObject.asTextField.text = "注册成功";
        }

        //请求创建角色 参数：角色位置 角色名 骨骼编号 职业编号
        public static async ETVoid CreateNewCharacter(int seat, string name, int skeleton, int career)
        {
            //模型本身由复数的部件构成 玩家先加载模型编号指定的默认模型再根据身上穿着的装备进行更换装备
            SkeletonType skeletonType =  SkeletonType.Man; ;
            CareerType careerType = CareerType.Warror;

            //将控制器页数转换为消息参数
            string gender = "";
            switch (skeleton)
            {
                case 0:
                    gender = "男";
                    skeletonType = SkeletonType.Man;
                    break;
                case 1:
                    gender = "女";
                    skeletonType = SkeletonType.Women;
                    break;
                default:
                    break;
            }

            switch (career)
            {
                case 0:
                    Log.Debug("创建" + gender + "战士" + " 名称：" + name);
                    careerType = CareerType.Warror;
                    break;
                case 1:
                    Log.Debug("创建" + gender + "法师" + " 名称：" + name);
                    careerType = CareerType.Mage;
                    break;
                default:
                    break;
            }
            
            CreateCharacterComponent creater = Game.Scene.GetComponent<FUIComponent>().Get(FUIType.CreateCharacter).GetComponent<CreateCharacterComponent>();
            
            //发送创建角色请求
            A0009_CreateNewCharacter_G2C messageCreate = (A0009_CreateNewCharacter_G2C)await SessionComponent.Instance.Session.Call(new A0009_CreateNewCharacter_C2G() {
                Name = name,
                Skeleton = skeletonType,
                Career = careerType,
                Seat = seat
            });

            //判断Gate服务器返回结果
            if (messageCreate.Error == ErrorCode.ERR_CreateNewCharacter)
            {
                creater.Prompt.GObject.asTextField.text = "名称已被占用";
                creater.NameInput.Get("Input").GObject.asTextInput.text = "";
                creater.isCreatingCharacter = false;
                return;
            }
            //判断通过则创建角色成功
            
            //获取角色信息判断应该进入哪个界面
            A0008_GetUserInfo_G2C messageUser = (A0008_GetUserInfo_G2C)await SessionComponent.Instance.Session.Call(new A0008_GetUserInfo_C2G());

            if (messageUser.Characters.Count == 3 &&
                messageUser.Characters[0].Level == 0 &&
                messageUser.Characters[1].Level == 0 &&
                messageUser.Characters[2].Level == 0)
            {
                //报错
                Log.Error("没有正确创建角色");
            }
            else if (messageUser.Characters.Count == 3 &&
                (messageUser.Characters[0].Level > 0 ||
                messageUser.Characters[1].Level > 0 ||
                messageUser.Characters[2].Level > 0))
            {
                //进入角色选择界面
                SelectCharacterFactory.Create(messageUser);
            }

            Game.EventSystem.Run(EventIdType.CreateCharacterFinish);
        }

        #region 配置
        //骨骼模型 并不包含mesh和材质 骨骼需要与模型配套 本例中没有严格遵守配套规则
        public static string GetSkeletonName(SkeletonType skeletonType)
        {
            //获取Prefab的方法：
            //一：表格 便于策划填表和修改 每次修改后需要重新导表 物体顺序基于表格中同类顺序
            //二：获取enum名 在代码中直接定义 Enum.GetName(color.GetType(), color); 需要严格遵守文件名规则
            //三：本例中的赋值操作 需要手动赋值一次

            string skeleton = "";
            switch (skeletonType)
            {
                case SkeletonType.Man:
                    skeleton = "ch_pc_hou_ZhanShi";
                    break;
                case SkeletonType.Women:
                    skeleton = "ch_pc_hou_FaShi";
                    break;
                default: //不存在的记录将读取资源报错
                    break;
            }
            return skeleton;
        }

        public static string GetWeaponName(WeaponType weaponType)
        {
            string weapon = "";
            switch (weaponType)
            {
                case WeaponType.NoneWeapon: //充数
                    weapon = "ch_we_one_hou_008";
                    break;
                case WeaponType.Sword:
                    weapon = "ch_we_one_hou_004";
                    break;
                case WeaponType.Wand:
                    weapon = "ch_we_one_hou_006";
                    break;
                default:
                    break;
            }
            return weapon;
        }

        public static string GetHeadName(HeadType headType)
        {
            string head = "";
            switch (headType)
            {
                case HeadType.NoneHead: //充数
                    head = "ch_pc_hou_008_tou";
                    break;
                case HeadType.Head1:
                    head = "ch_pc_hou_004_tou";
                    break;
                case HeadType.Head2:
                    head = "ch_pc_hou_006_tou";
                    break;
                default:
                    break;
            }
            return head;
        }

        public static string GetChestName(ChestType chestType)
        {
            string chest = "";
            switch (chestType)
            {
                case ChestType.NoneChest: //充数
                    chest = "ch_pc_hou_008_shen";
                    break;
                case ChestType.Chest1:
                    chest = "ch_pc_hou_004_shen";
                    break;
                case ChestType.Chest2:
                    chest = "ch_pc_hou_006_shen";
                    break;
                default:
                    break;
            }
            return chest;
        }

        public static string GetHandName(HandType handType)
        {
            string hand = "";
            switch (handType)
            {
                case HandType.NoneHand: //充数
                    hand = "ch_pc_hou_008_shou";
                    break;
                case HandType.Hand1:
                    hand = "ch_pc_hou_004_shou";
                    break;
                case HandType.Hand2:
                    hand = "ch_pc_hou_006_shou";
                    break;
                default:
                    break;
            }
            return hand;
        }

        public static string GetFeetName(FeetType feetType)
        {
            string feet = "";
            switch (feetType)
            {
                case FeetType.NoneFeet: //充数
                    feet = "ch_pc_hou_008_jiao";
                    break;
                case FeetType.Feet1:
                    feet = "ch_pc_hou_004_jiao";
                    break;
                case FeetType.Feet2:
                    feet = "ch_pc_hou_006_jiao";
                    break;
                default:
                    break;
            }
            return feet;
        }

        //合并mesh到骨骼
        public static void CombineObject(GameObject skeleton, SkinnedMeshRenderer[] meshes, bool combine = false)
        {
            int COMBINE_TEXTURE_MAX = 512;
            string COMBINE_DIFFUSE_TEXTURE = "_MainTex";

            // Fetch all bones of the skeleton
            // transforms是骨架上所有的transform组件的List
            List<Transform> transforms = new List<Transform>();
            transforms.AddRange(skeleton.GetComponentsInChildren<Transform>(true));

            // the list of materials
            // materials是所有身体部件上的所有Material组成的List
            List<Material> materials = new List<Material>();

            // the list of meshes
            // combineInstances是所有身体部件上所有的mesh组成的List
            List<CombineInstance> combineInstances = new List<CombineInstance>();

            //the list of bones
            List<Transform> bones = new List<Transform>();

            // Below informations only are used for merge materilas(bool combine = true)
            List<Vector2[]> oldUV = null;
            Material newMaterial = null;
            Texture2D newDiffuseTex = null;

            // Collect information from meshes
            // 这里分别把所有Material，Mesh，Transform(骨头)保存到对应的List
            for (int i = 0; i < meshes.Length; i++)
            {
                SkinnedMeshRenderer smr = meshes[i];
                // Collect materials
                materials.AddRange(smr.materials);

                // Collect meshes
                for (int sub = 0; sub < smr.sharedMesh.subMeshCount; sub++)
                {
                    CombineInstance ci = new CombineInstance();
                    ci.mesh = smr.sharedMesh;
                    ci.subMeshIndex = sub;
                    combineInstances.Add(ci);
                }

                // Collect bones
                // 收集骨头有点区别：只收集骨架中有的。这应该会涉及到具体的美术标准。
                for (int j = 0; j < smr.bones.Length; j++)
                {
                    int tBase = 0;
                    for (tBase = 0; tBase < transforms.Count; tBase++)
                    {
                        if (smr.bones[j].name.Equals(transforms[tBase].name))
                        {
                            //Debug.Log("Equals bones " + smr.bones[j].name);
                            bones.Add(transforms[tBase]);
                            break;
                        }
                    }
                }
            }

            // merge materials
            // Material合并，主要是处理贴图和其UV
            if (combine)
            {
                newMaterial = new Material(Shader.Find("Mobile/Diffuse"));
                oldUV = new List<Vector2[]>();
                // merge the texture
                // 合并贴图（从收集的各Material中取）
                // Textures是所有贴图组成的列表
                List<Texture2D> Textures = new List<Texture2D>();
                for (int i = 0; i < materials.Count; i++)
                {
                    Textures.Add(materials[i].GetTexture(COMBINE_DIFFUSE_TEXTURE) as Texture2D);
                }

                //所有贴图合并到newDiffuseTex这张大贴图上
                newDiffuseTex = new Texture2D(COMBINE_TEXTURE_MAX, COMBINE_TEXTURE_MAX, TextureFormat.RGBA32, true);
                Rect[] uvs = newDiffuseTex.PackTextures(Textures.ToArray(), 0);
                newMaterial.mainTexture = newDiffuseTex;

                // reset uv
                // 根据原来单个上的uv算出合并后的uv，uva是单个的，uvb是合并后的。
                // uva取自combineInstances[j].mesh.uv
                // 用oldUV保存uva。为什么要保存uva？它不是单个吗？先跳过往下看
                // 计算好uvb赋值到到combineInstances[j].mesh.uv
                Vector2[] uva, uvb;
                for (int j = 0; j < combineInstances.Count; j++)
                {
                    //uva = (Vector2[])(combineInstances[j].mesh.uv);
                    uva = combineInstances[j].mesh.uv;
                    uvb = new Vector2[uva.Length];
                    for (int k = 0; k < uva.Length; k++)
                    {
                        uvb[k] = new Vector2((uva[k].x * uvs[j].width) + uvs[j].x, (uva[k].y * uvs[j].height) + uvs[j].y);
                    }
                    //oldUV.Add(combineInstances[j].mesh.uv);
                    oldUV.Add(uva);
                    combineInstances[j].mesh.uv = uvb;
                }
            }

            // Create a new SkinnedMeshRenderer
            SkinnedMeshRenderer oldSKinned =
                skeleton.GetComponent<SkinnedMeshRenderer>();
            if (oldSKinned != null)
            {
                GameObject.DestroyImmediate(oldSKinned);
            }
            SkinnedMeshRenderer r = skeleton.AddComponent<SkinnedMeshRenderer>();
            r.sharedMesh = new Mesh();
            // Combine meshes
            r.sharedMesh.CombineMeshes(combineInstances.ToArray(), combine, false);
            // Use new bones
            r.bones = bones.ToArray();
            if (combine)
            {
                //Debug.Log("combine " + combine);
                r.material = newMaterial;
                for (int i = 0; i < combineInstances.Count; i++)
                {
                    // 这为什么要用oldUV，这不是保存的uva吗？它是单个的uv呀？
                    // 原因在于，这行代码其实并不影响显示，影响显示的是在Mesh合并前的uv。
                    // 这行的意义在于合并后，又要换部件时，在新的合并过程中找到正确的单个uv。
                    // 也是oldUV存在的意义。
                    combineInstances[i].mesh.uv = oldUV[i];
                }
            }
            else
            {
                Debug.Log("combine " + combine);
                r.materials = materials.ToArray();
            }
        }

        //使用指定部件创建GameObject
        public static GameObject CreateCharacter(string skeleton, string weapon, string head, string chest, string hand, string feet)
        {
            //创建骨骼
            UnityEngine.Object res = Resources.Load("Sekia/Prefab/" + skeleton);
            GameObject Instance = GameObject.Instantiate(res) as GameObject;

            //创建身体部件
            string[] equipments = new string[4];
            equipments[0] = head;
            equipments[1] = chest;
            equipments[2] = hand;
            equipments[3] = feet;
            SkinnedMeshRenderer[] meshes = new SkinnedMeshRenderer[4];
            GameObject[] objects = new GameObject[4];
            for (int i = 0; i < equipments.Length; i++)
            {
                res = Resources.Load("Sekia/Prefab/" + equipments[i]);
                objects[i] = GameObject.Instantiate(res) as GameObject;
                meshes[i] = objects[i].GetComponentInChildren<SkinnedMeshRenderer>();
            }

            //合并身体mesh
            CombineObject(Instance, meshes, true);

            //删除临时身体部件资源
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject.DestroyImmediate(objects[i].gameObject);
            }

            //创建武器部件
            res = Resources.Load("Sekia/Prefab/" + weapon);
            GameObject WeaponInstance = GameObject.Instantiate(res) as GameObject;

            //设置武器位置
            Transform[] transforms = Instance.GetComponentsInChildren<Transform>();
            foreach (Transform joint in transforms)
            {
                if (joint.name == "weapon_hand_r")
                {// find the joint (need the support of art designer)
                    WeaponInstance.transform.parent = joint.gameObject.transform;
                    break;
                }
            }
            WeaponInstance.transform.localScale = Vector3.one;
            WeaponInstance.transform.localPosition = Vector3.zero;
            WeaponInstance.transform.localRotation = Quaternion.identity;

            return Instance;
        }

        #endregion
    }
}