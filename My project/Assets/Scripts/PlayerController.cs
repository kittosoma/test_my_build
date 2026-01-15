using UnityEngine;
using Spine.Unity;
using Spine;

public class PlayerController : MonoBehaviour
{
    [Header("Spine Animation")]
    [SerializeField] private SkeletonAnimation skeletonAnimation;
    public AnimationReferenceAsset runAnimation;
    public AnimationReferenceAsset slipAnimation;
    public AnimationReferenceAsset idleAnimation;
    
    [Header("Root Motion")]
    public AnimationReferenceAsset rootMotionAnimation;
    [SerializeField] private SkeletonRootMotion skeletonRootMotion;
    
    [Header("Movement Settings")]
    [SerializeField] private float maxMoveSpeed = 5f; // 최대 이동 속도
    [SerializeField] private float acceleration = 20f; // 가속도 (높을수록 빠르게 가속)
    [SerializeField] private float deceleration = 15f; // 감속도 (높을수록 빠르게 감속)
    [SerializeField] private float slipDeceleration = 10f; // slip 시 감속도
    
    [Header("Animation Speed Settings")]
    [SerializeField] private float minAnimationSpeed = 0.5f; // 최소 애니메이션 속도 (느리게 시작)
    [SerializeField] private float maxAnimationSpeed = 1.0f; // 최대 애니메이션 속도 (최고 속도일 때)
    
    private bool isFacingRight = true;
    private bool isSlipping = false;
    private bool hasSlipped = false;
    private float currentSpeed = 0f; // 현재 이동 속도
    private float slipVelocity = 0f;
    private float slipStartTime = 0f;
    private float slipDuration = 0f;
    private bool isPlayingRootMotion = false;
    private Vector3 rootBoneStartWorldPosition;
    private Vector3 testPlayerStartPosition;
    private Bone rootBone;
    
    void Start()
    {
        // SkeletonAnimation 컴포넌트가 할당되지 않았다면 자동으로 찾기
        if (skeletonAnimation == null)
        {
            skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
        }
        
        // SkeletonRootMotion 컴포넌트가 할당되지 않았다면 자동으로 찾기
        if (skeletonRootMotion == null)
        {
            skeletonRootMotion = GetComponent<SkeletonRootMotion>();
        }
        
        // root 본 찾기
        if (skeletonAnimation != null)
        {
            rootBone = skeletonAnimation.Skeleton.FindBone("root");
            if (rootBone == null)
            {
                Debug.LogError("Root bone not found!");
            }
        }
    }
    
    void Update()
    {
        // 스페이스바로 Root Motion 애니메이션 실행
        if (Input.GetKeyDown(KeyCode.Space) && rootMotionAnimation != null && !isPlayingRootMotion)
        {
            PlayRootMotionAnimation();
        }
        
        // Root Motion 중에는 일반 이동 처리 안함
        if (!isPlayingRootMotion)
        {
            HandleMovement();
        }
    }
    
    void LateUpdate()
    {
        // Root Motion 중에 root 본의 로컬 위치 변화를 추적
        if (isPlayingRootMotion && rootBone != null)
        {
            // root 본의 현재 로컬 위치 (부모 본 기준)
            Vector3 rootBoneLocalPos = new Vector3(rootBone.X, rootBone.Y, 0);
            Vector3 totalDelta = rootBoneLocalPos - rootBoneStartWorldPosition;
            
            // Spine 좌표를 Unity 월드 좌표로 변환 (스케일 적용)
            Vector3 worldDelta = skeletonAnimation.transform.TransformVector(totalDelta);
            
            Debug.Log($"[LateUpdate] Root bone local: {rootBoneLocalPos}, Start: {rootBoneStartWorldPosition}, Delta: {totalDelta}, World Delta: {worldDelta}");
            
            // Test_player를 시작 위치 + root 본의 총 이동량으로 설정
            transform.position = testPlayerStartPosition + worldDelta;
            
            // Player 자식 오브젝트를 반대 방향으로 움직여서 시각적 이동 상쇄
            skeletonAnimation.transform.localPosition = -totalDelta;
        }
    }
    
    void PlayRootMotionAnimation()
    {
        if (skeletonAnimation != null && rootMotionAnimation != null && rootBone != null)
        {
            isPlayingRootMotion = true;
            
            // Test_player의 시작 위치 저장
            testPlayerStartPosition = transform.position;
            
            // root 본의 시작 로컬 위치 저장
            rootBoneStartWorldPosition = new Vector3(rootBone.X, rootBone.Y, 0);
            Debug.Log($"[Root Motion Start] Test_player Position: {testPlayerStartPosition}, Root bone local: {rootBoneStartWorldPosition}");
            
            // Root Motion 컴포넌트는 비활성화 (우리가 직접 처리)
            if (skeletonRootMotion != null)
            {
                skeletonRootMotion.enabled = false;
            }
            
            var trackEntry = skeletonAnimation.AnimationState.SetAnimation(0, rootMotionAnimation, false);
            
            if (trackEntry != null)
            {
                // Root Motion 애니메이션이 끝나면 처리
                trackEntry.Complete += (entry) =>
                {
                    Debug.Log($"[Root Motion Complete] Test_player Position: {transform.position}");
                    
                    // idle 애니메이션 재생
                    if (idleAnimation != null)
                    {
                        skeletonAnimation.AnimationState.SetAnimation(0, idleAnimation, true);
                    }
                    
                    // 다음 프레임에 정리 작업
                    StartCoroutine(CleanupAfterRootMotion());
                };
            }
        }
    }
    
    System.Collections.IEnumerator CleanupAfterRootMotion()
    {
        yield return null; // 1프레임 대기
        
        // Player 자식 오브젝트를 원래 위치로 복원
        skeletonAnimation.transform.localPosition = Vector3.zero;
        
        isPlayingRootMotion = false;
        
        Debug.Log($"[Cleanup] Player local position reset, isPlayingRootMotion = false");
    }
    
    void HandleMovement()
    {
        float horizontalInput = 0f;
        
        // A키 (왼쪽)
        if (Input.GetKey(KeyCode.A))
        {
            horizontalInput = -1f;
        }
        // D키 (오른쪽)
        else if (Input.GetKey(KeyCode.D))
        {
            horizontalInput = 1f;
        }
        
        // 이동 처리
        if (horizontalInput != 0)
        {
            // 슬립 상태 즉시 해제
            if (isSlipping)
            {
                isSlipping = false;
            }
            hasSlipped = false;
            
            // 가속 처리
            float oldSpeed = currentSpeed;
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxMoveSpeed, acceleration * Time.deltaTime);
            
            if (Time.frameCount % 30 == 0) // 30프레임마다 한 번만 로그
            {
                Debug.Log($"Speed: {oldSpeed:F2} -> {currentSpeed:F2}, MaxSpeed: {maxMoveSpeed}, Accel: {acceleration}");
            }
            
            // 캐릭터 이동
            transform.Translate(Vector3.right * horizontalInput * currentSpeed * Time.deltaTime);
            
            // Run 애니메이션 재생
            PlayRunAnimation();
            
            // 방향 전환 (Initial Flip X 사용)
            if (horizontalInput > 0 && !isFacingRight)
            {
                Flip();
            }
            else if (horizontalInput < 0 && isFacingRight)
            {
                Flip();
            }
        }
        else if (!isSlipping && !hasSlipped && currentSpeed > 0.1f)
        {
            // 키를 떼고 속도가 있을 때만 slip 애니메이션 시작
            StartSlip();
        }
        else if (!isSlipping && currentSpeed <= 0.1f)
        {
            // 속도가 거의 없으면 idle 상태로
            currentSpeed = 0f;
            hasSlipped = false;
        }
        
        // 슬립 중 감속 이동 처리
        if (isSlipping)
        {
            HandleSlipMovement();
        }
    }
    
    void StartSlip()
    {
        Debug.Log("StartSlip called");
        isSlipping = true;
        hasSlipped = true;
        slipVelocity = currentSpeed; // 현재 속도를 slip 속도로 저장
        slipStartTime = Time.time;
        
        // slip 애니메이션 재생 및 길이 가져오기 (루프 없이 한 번만 재생)
        if (skeletonAnimation != null && slipAnimation != null)
        {
            var trackEntry = skeletonAnimation.AnimationState.SetAnimation(0, slipAnimation, false);
            if (trackEntry != null)
            {
                slipDuration = trackEntry.Animation.Duration;
                Debug.Log($"Slip animation duration: {slipDuration}");
                
                // slip 애니메이션이 끝나면 idle 애니메이션을 큐에 추가 (루프로 재생)
                if (idleAnimation != null)
                {
                    skeletonAnimation.AnimationState.AddAnimation(0, idleAnimation, true, 0f);
                }
            }
        }
    }
    
    void HandleSlipMovement()
    {
        if (!isSlipping) return;
        
        float elapsedTime = Time.time - slipStartTime;
        
        Debug.Log($"Slip - Elapsed: {elapsedTime:F2}, Duration: {slipDuration:F2}, IsSlipping: {isSlipping}");
        
        if (elapsedTime < slipDuration)
        {
            // 감속 처리 (slipDeceleration 사용)
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, slipDeceleration * Time.deltaTime);
            
            Debug.Log($"Moving - Speed: {currentSpeed:F2}");
            
            // 현재 바라보는 방향으로 이동
            float direction = isFacingRight ? 1f : -1f;
            transform.Translate(Vector3.right * direction * currentSpeed * Time.deltaTime);
        }
        else
        {
            // slip 애니메이션이 끝나면 슬립 상태 해제
            Debug.Log("Slip finished - Setting isSlipping to false");
            isSlipping = false;
            currentSpeed = 0f; // 속도 완전히 리셋
        }
    }
    
    void PlayRunAnimation()
    {
        if (skeletonAnimation != null && runAnimation != null)
        {
            var currentTrack = skeletonAnimation.AnimationState.GetCurrent(0);
            
            // 현재 run 애니메이션이 재생 중이 아니면 재생
            if (currentTrack == null || currentTrack.Animation != runAnimation.Animation)
            {
                skeletonAnimation.AnimationState.SetAnimation(0, runAnimation, true);
            }
            
            // 현재 속도에 따라 애니메이션 속도 조정
            float speedRatio = currentSpeed / maxMoveSpeed; // 0 ~ 1 사이 값
            float animSpeed = Mathf.Lerp(minAnimationSpeed, maxAnimationSpeed, speedRatio);
            
            currentTrack = skeletonAnimation.AnimationState.GetCurrent(0);
            if (currentTrack != null)
            {
                currentTrack.TimeScale = animSpeed;
            }
        }
    }
    
    void PlayIdleAnimation()
    {
        if (skeletonAnimation != null && idleAnimation != null)
        {
            var currentTrack = skeletonAnimation.AnimationState.GetCurrent(0);
            
            // 현재 idle 애니메이션이 재생 중이 아니면 재생
            if (currentTrack == null || currentTrack.Animation != idleAnimation.Animation)
            {
                skeletonAnimation.AnimationState.SetAnimation(0, idleAnimation, true);
            }
        }
    }
    
    void Flip()
    {
        isFacingRight = !isFacingRight;
        
        // Transform의 localScale을 사용하여 방향 전환
        Vector3 scale = transform.localScale;
        scale.x = isFacingRight ? 1 : -1;
        transform.localScale = scale;
    }
}
