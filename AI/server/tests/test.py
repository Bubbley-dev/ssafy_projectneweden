import requests
import json
import time
import os

# 전역 세션 객체 생성
session = requests.Session()

# ==============================
#  서버 호출 함수
# ==============================
def get_response(prompt: str) -> str:
    """
    prompt 문자열을 LLM 서버에 보내고,
    JSON 응답에서 'response' 필드를 꺼내 출력 후 반환합니다.
    """
    payload = {
        "model": "gemma3",
        "prompt": prompt,
        "stream": False  # 스트리밍 모드를 꺼서 단일 JSON 응답을 받습니다
    }
    # HTTP 요청 준비
    data = json.dumps(payload).encode('utf-8')
    
    # 요청-응답 시간 측정 시작
    start_time = time.time()
    try:
        res = session.post(
            API_URL,
            data=data,
            headers={'Content-Type': 'application/json'}
        )
        res.raise_for_status()  # HTTP 오류 체크
        
        # Response 객체의 text 속성 사용
        result = json.loads(res.text)
        answer = result.get("response", "")
        
        # 결과 출력
        print("\n🧠 응답:")
        print(answer)
        print(f"\n⏱ 응답시간: {time.time() - start_time:.3f}초\n")
        
        return answer
        
    except requests.exceptions.RequestException as e:
        print(f"\n❌ 오류 발생: {str(e)}")
        return ""
    except json.JSONDecodeError as e:
        print(f"\n❌ JSON 파싱 오류: {str(e)}")
        return ""
    except Exception as e:
        print(f"\n❌ 예상치 못한 오류: {str(e)}")
        return ""

# ==============================
#  스크립트 진입점
# ==============================
if __name__ == "__main__":
    # 서버 주소 설정
    PORT = 11434
    HOST = 'localhost'
    API_URL = f"http://{HOST}:{PORT}/api/generate"

    print("=== AI 챗봇 테스트 시작 ===")
    print("프롬프트 파일을 사용하여 테스트합니다.")
    
    # 현재 스크립트의 디렉토리 경로를 기준으로 프롬프트 파일 경로 설정
    current_dir = os.path.dirname(os.path.abspath(__file__))
    prompt_path = os.path.join(current_dir, "prompt.txt")
    
    try:
        # 프롬프트 로드
        with open(prompt_path, 'r', encoding='utf-8') as f:
            prompt = f.read().strip()
        
        # 프롬프트 출력
        print("\n===== LOADED PROMPT =====")
        print(prompt)
        print("===== END PROMPT =====\n")
        
        # LLM 호출
        get_response(prompt)
        
    except FileNotFoundError as e:
        print(f"\n❌ 파일을 찾을 수 없습니다: {str(e)}")
        print(f"찾으려는 파일 경로: {prompt_path}")
    except Exception as e:
        print(f"\n❌ 오류 발생: {str(e)}")
