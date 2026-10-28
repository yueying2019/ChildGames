namespace SmallGames
{
    public enum LegalDocId
    {
        PrivacyPolicy = 0,
        UserAgreement = 1,
        HealthAdvice = 2,
        AgeHint = 3
    }

    /// <summary>
    /// 微信小游戏开场、适龄与隐私文案。上架前请把运营方、联系方式改成真实信息。
    /// </summary>
    public static class ComplianceCatalog
    {
        public const int PrivacyVersion = 2;
        public const string AgeRating = "8+";
        public const string OperatorName = "咔滋盒运营方";
        public const string Contact = "请通过微信公众平台小游戏账号与我们联系";
        public const string RecordNumber = "备案号以微信公众平台展示为准";
        public const float SplashSeconds = 3f;

        public static readonly string HealthTitle = "健康游戏忠告";
        public static readonly string HealthBody =
            "抵制不良游戏，拒绝盗版游戏。注意自我保护，谨防受骗上当。\n" +
            "适度游戏益脑，沉迷游戏伤身。合理安排时间，享受健康生活。";

        public static readonly string AgeTitle = "适龄提示 " + AgeRating;
        public static readonly string AgeBody =
            "本游戏为休闲益智类玩法，无暴力、恐怖、不良语言或社交内容，适合 8 周岁以上用户使用。\n" +
            "请监护人合理安排未成年人的游戏时间，并开启设备或平台提供的未成年人保护功能。";

        public static readonly string PrivacyPopupBody =
            "欢迎使用《咔滋盒》。本游戏是单机消除，不获取头像、昵称、位置、相册，也不接入广告或支付。\n\n" +
            "关卡进度、金币和设置只保存在本机。请阅读并同意《用户协议》和《隐私政策》后进入游戏。未勾选时无法默认同意。";

        public static string TitleOf(LegalDocId doc)
        {
            switch (doc)
            {
                case LegalDocId.UserAgreement: return "用户协议";
                case LegalDocId.HealthAdvice: return HealthTitle;
                case LegalDocId.AgeHint: return AgeTitle;
                default: return "隐私政策";
            }
        }

        public static string BodyOf(LegalDocId doc)
        {
            switch (doc)
            {
                case LegalDocId.UserAgreement: return UserAgreement;
                case LegalDocId.HealthAdvice: return HealthTitle + "\n\n" + HealthBody;
                case LegalDocId.AgeHint: return AgeTitle + "\n\n" + AgeBody;
                default: return PrivacyPolicy;
            }
        }

        public static readonly string PrivacyPolicy =
            "《咔滋盒》隐私政策\n" +
            "更新日期：2026年10月11日    生效日期：2026年10月11日\n\n" +
            "1. 引言\n" +
            "咔滋盒（下称“本游戏”）由" + OperatorName + "（下称“我们”）提供。当前版本是单机休闲消除，无登录、无广告、无支付。本政策说明本版本实际处理的信息。微信内请同时查阅微信《用户隐私保护指引》。\n\n" +
            "2. 本版本不收集的信息\n" +
            "我们不获取微信头像、昵称、性别、地区、手机号、身份证号、地理位置、通讯录、相册、剪贴板，也不使用排行榜、分享或广告接口。未使用的能力不会申请权限。\n\n" +
            "3. 仅保存在本机的数据\n" +
            "3.1 为了继续游戏，关卡进度、星级、金币、体力、道具、设置、引导、签到和每日任务状态保存在本机。这些数据不会上传到我们的服务器。\n" +
            "3.2 对局次数等玩法统计也只写在本机文件中，不包含设备标识，不会上报。\n" +
            "3.3 卸载小游戏或清除缓存后，上述数据可能被删除且无法恢复。\n\n" +
            "4. 使用目的\n" +
            "仅用于提供游戏功能、保存进度，以及符合法律法规和未成年人保护要求。我们不会把这些数据用于营销，也不会出售或向第三方提供。\n\n" +
            "5. 微信平台\n" +
            "本游戏运行在微信小游戏中。微信作为平台方可能按其自身规则处理运行所必需的信息，该处理适用微信的隐私规则，不由本政策扩大。\n\n" +
            "6. 未成年人保护\n" +
            "本游戏适龄提示为 8+。不满 18 周岁的用户应在监护人指导下使用。我们不会主动向未成年人收集个人信息。\n\n" +
            "7. 你的权利\n" +
            "你可以在设置中再次打开本政策。删除本地数据可清除小游戏缓存。我们不会以默认勾选方式取得同意。\n\n" +
            "8. 政策变更\n" +
            "若以后接入登录、广告、支付或把数据上传到服务器，我们会更新本政策、提高版本号，并在进入游戏前再次征求同意。\n\n" +
            "9. 联系我们\n" +
            "运营方：" + OperatorName + "\n" +
            "联系方式：" + Contact + "\n" +
            RecordNumber;

        public static readonly string UserAgreement =
            "《咔滋盒》用户协议\n" +
            "更新日期：2026年10月11日    生效日期：2026年10月11日\n\n" +
            "1. 接受条款\n" +
            "你使用本游戏即表示已阅读并同意本协议和《隐私政策》。若不同意，请停止使用。\n\n" +
            "2. 使用规范\n" +
            "你应以合法、正当的方式使用本游戏，不得利用本游戏从事违法违规、侵犯他人权益、破坏游戏公平或干扰服务的行为，包括但不限于外挂、作弊、反向工程、恶意攻击。\n\n" +
            "3. 游戏内容与虚拟物品\n" +
            "本游戏提供对对消、叠叠消、三消等休闲玩法。金币、体力等虚拟道具仅可在游戏内使用，不具有兑换法定货币的属性，除法律法规另有规定外不可转让。\n\n" +
            "4. 健康游戏\n" +
            HealthBody + "\n" +
            "请合理安排游戏时间。\n\n" +
            "5. 适龄提示\n" +
            AgeBody + "\n\n" +
            "6. 免责声明\n" +
            "受网络、设备、平台政策影响，服务可能中断或变更。在法律允许范围内，我们对间接损失、数据丢失不作超出法定义务的赔偿。因你违反本协议导致的损失由你自行承担。\n\n" +
            "7. 协议变更与终止\n" +
            "我们可能更新本协议。你有权停止使用。本版本没有账号。我们也可在必要时停止向违规使用本游戏的用户提供服务。\n\n" +
            "8. 其他\n" +
            "本协议适用中华人民共和国法律。争议协商不成的，由运营方所在地有管辖权的人民法院管辖。\n\n" +
            "运营方：" + OperatorName + "\n" +
            RecordNumber;
    }
}
