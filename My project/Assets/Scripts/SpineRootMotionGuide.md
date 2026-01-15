# Spine Root Motion 구현 가이드

이 문서는 Unity에서 Spine 애니메이션의 Root Motion을 커스텀으로 구현하는 방법을 설명합니다.

## 개요

Spine의 공식 `Skeleton Root Motion` 컴포넌트는 부모-자식 계층 구조에서 제대로 작동하지 않을 수 있습니다. 이 가이드는 root 본의 이동을 직접 추적하여 부모 오브젝트를 움직이는 커스텀 솔루션을 제공합니다.

## 문제 상황

- **오브젝트 구조**: `ParentObject` (이동해야 함) → `ChildObject` (SkeletonAnimation 포함)
- **Spine Root Motion 컴포넌트의 한계**: 자신이 붙은 오브젝트만 움직일 수 있음
- **시각적 문제**: Root Motion과 애니메이션의 시각적 이동이 중복되어 2배로 보임

## 해결 방법

### 1. 오브젝트 계층 구조

```
ParentObject (이동할 오브젝트)
└── ChildObject (SkeletonAnimation 컴포넌트)
```

### 2. 필요한 변수

```csharp
[Header("Root Motion")]
public AnimationReferenceAsset rootMotionAnimation;
[SerializeField] private SkeletonAnimation skeletonAnimation;
[SerializeField] private SkeletonRootMotion skeletonRootMotion; // 비활성화용

private bool isPlayingRootMotion = false;
private Vector3 rootBoneStartWorldPosition;
private Vector3 testPlayerStartPosition;
private Bone rootBone;
```

### 3. 초기화 (Start)

```csharp
void Start()
{
    // SkeletonAnimation 찾기
    if (skeletonAnimation == null)
    {
        skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
    }
    
    // Skeleton Root Motion 컴포넌트 찾기 (비활성화용)
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
```

### 4. Root Motion 실행

```csharp
void PlayRootMotionAnimation()
{
    if (skeletonAnimation != null && rootMotionAnimation != null && rootBone != null)
    {
        isPlayingRootMotion = true;
        
        // 부모 오브젝트의 시작 위치 저장
        testPlayerStartPosition = transform.position;
        
        // root 본의 시작 로컬 위치 저장
        rootBoneStartWorldPosition = new Vector3(rootBone.X, rootBone.Y, 0);
        
        // Spine의 Root Motion 컴포넌트 비활성화 (충돌 방지)
        if (skeletonRootMotion != null)
        {
            skeletonRootMotion.enabled = false;
        }
        
        var trackEntry = skeletonAnimation.AnimationState.SetAnimation(0, rootMotionAnimation, false);
        
        if (trackEntry != null)
        {
            trackEntry.Complete += (entry) =>
            {
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
    
    // 자식 오브젝트를 원래 위치로 복원
    skeletonAnimation.transform.localPosition = Vector3.zero;
    
    isPlayingRootMotion = false;
}
```

### 5. LateUpdate에서 위치 업데이트

```csharp
void LateUpdate()
{
    // Root Motion 중에 root 본의 로컬 위치 변화를 추적
    if (isPlayingRootMotion && rootBone != null)
    {
        // root 본의 현재 로컬 위치
        Vector3 rootBoneLocalPos = new Vector3(rootBone.X, rootBone.Y, 0);
        Vector3 totalDelta = rootBoneLocalPos - rootBoneStartWorldPosition;
        
        // Spine 좌표를 Unity 월드 좌표로 변환
        Vector3 worldDelta = skeletonAnimation.transform.TransformVector(totalDelta);
        
        // 부모 오브젝트를 시작 위치 + root 본의 총 이동량으로 설정
        transform.position = testPlayerStartPosition + worldDelta;
        
        // 자식 오브젝트를 반대 방향으로 움직여서 시각적 이동 상쇄
        // (애니메이션의 시각적 이동과 실제 Transform 이동이 중복되는 것을 방지)
        skeletonAnimation.transform.localPosition = -totalDelta;
    }
}
```

## 핵심 원리

### 1. 이중 이동 문제 해결

Root Motion 애니메이션이 재생되면:
- **애니메이션 자체**가 root 본을 시각적으로 이동시킴
- **우리 스크립트**가 부모 오브젝트를 실제로 이동시킴

이 두 이동이 합쳐져서 2배로 보이는 문제를 해결하기 위해:
```csharp
// 부모는 앞으로 이동
transform.position = testPlayerStartPosition + worldDelta;

// 자식은 뒤로 이동 (시각적 상쇄)
skeletonAnimation.transform.localPosition = -totalDelta;
```

### 2. 좌표계 변환

Spine의 로컬 좌표를 Unity의 월드 좌표로 변환:
```csharp
Vector3 worldDelta = skeletonAnimation.transform.TransformVector(totalDelta);
```

이렇게 하면 스케일, 회전이 자동으로 적용됩니다.

### 3. 방향 전환 (Flip)

Root Motion과 함께 사용하려면 Transform의 localScale을 사용해야 합니다:

```csharp
void Flip()
{
    isFacingRight = !isFacingRight;
    
    // Transform의 localScale을 사용하여 방향 전환
    Vector3 scale = transform.localScale;
    scale.x = isFacingRight ? 1 : -1;
    transform.localScale = scale;
}
```

**주의**: `Skeleton.ScaleX`를 사용하면 Root Motion의 방향이 반전되지 않습니다!

## 다른 캐릭터에 적용하기

### 1. 기본 템플릿

```csharp
public class CustomRootMotionController : MonoBehaviour
{
    [Header("Spine Animation")]
    [SerializeField] private SkeletonAnimation skeletonAnimation;
    
    [Header("Root Motion")]
    public AnimationReferenceAsset rootMotionAnimation;
    public AnimationReferenceAsset idleAnimation;
    
    private bool isPlayingRootMotion = false;
    private Vector3 rootBoneStartPosition;
    private Vector3 startPosition;
    private Bone rootBone;
    
    void Start()
    {
        if (skeletonAnimation == null)
            skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
            
        if (skeletonAnimation != null)
            rootBone = skeletonAnimation.Skeleton.FindBone("root");
    }
    
    void LateUpdate()
    {
        if (isPlayingRootMotion && rootBone != null)
        {
            Vector3 currentPos = new Vector3(rootBone.X, rootBone.Y, 0);
            Vector3 delta = currentPos - rootBoneStartPosition;
            Vector3 worldDelta = skeletonAnimation.transform.TransformVector(delta);
            
            transform.position = startPosition + worldDelta;
            skeletonAnimation.transform.localPosition = -delta;
        }
    }
    
    public void PlayRootMotion()
    {
        if (rootBone == null) return;
        
        isPlayingRootMotion = true;
        startPosition = transform.position;
        rootBoneStartPosition = new Vector3(rootBone.X, rootBone.Y, 0);
        
        var track = skeletonAnimation.AnimationState.SetAnimation(0, rootMotionAnimation, false);
        track.Complete += (entry) =>
        {
            skeletonAnimation.AnimationState.SetAnimation(0, idleAnimation, true);
            StartCoroutine(Cleanup());
        };
    }
    
    System.Collections.IEnumerator Cleanup()
    {
        yield return null;
        skeletonAnimation.transform.localPosition = Vector3.zero;
        isPlayingRootMotion = false;
    }
}
```

### 2. 여러 Root Motion 애니메이션 사용

```csharp
public void PlayRootMotion(AnimationReferenceAsset animation, AnimationReferenceAsset nextAnimation)
{
    if (rootBone == null) return;
    
    isPlayingRootMotion = true;
    startPosition = transform.position;
    rootBoneStartPosition = new Vector3(rootBone.X, rootBone.Y, 0);
    
    var track = skeletonAnimation.AnimationState.SetAnimation(0, animation, false);
    track.Complete += (entry) =>
    {
        skeletonAnimation.AnimationState.SetAnimation(0, nextAnimation, true);
        StartCoroutine(Cleanup());
    };
}
```

## 주의사항

1. **root 본 이름**: Spine 에디터에서 root 본의 이름이 "root"인지 확인하세요.

2. **LateUpdate 사용**: 반드시 `LateUpdate`를 사용해야 Spine 애니메이션 업데이트 후에 위치를 조정할 수 있습니다.

3. **코루틴 정리**: Root Motion이 끝난 후 1프레임 대기해야 idle 애니메이션과 부드럽게 전환됩니다.

4. **Skeleton Root Motion 컴포넌트**: 프리팹에 있다면 비활성화하거나 제거하세요.

5. **방향 전환**: Root Motion을 사용할 때는 반드시 Transform의 localScale을 사용하세요.

## 트러블슈팅

### 캐릭터가 2배로 이동해요
→ `skeletonAnimation.transform.localPosition = -totalDelta;` 라인이 있는지 확인하세요.

### 캐릭터가 제자리로 돌아와요
→ `CleanupAfterRootMotion()` 코루틴이 제대로 호출되는지 확인하세요.

### 방향 전환 시 Root Motion이 반대로 가요
→ `Skeleton.ScaleX` 대신 `transform.localScale.x`를 사용하세요.

### 애니메이션 전환 시 틱이 발생해요
→ `Complete` 이벤트에서 idle 재생 후 코루틴으로 정리 작업을 하세요.

## 참고

- Spine Unity Runtime 문서: https://esotericsoftware.com/spine-unity
- 본 가이드는 Spine Unity Runtime 4.1+ 기준으로 작성되었습니다.
