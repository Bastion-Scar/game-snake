package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"mime/multipart"
	"net/http"
	"os"
	"strings"
	"time"

	"github.com/go-resty/resty/v2"
	"github.com/joho/godotenv"
)

type YandexMessage struct {
	UserID string `json:"user_id"`
	Text   string `json:"text"`
}

type JiraIssue struct {
	Fields JiraFields `json:"fields"`
}

type JiraFields struct {
	Project     JiraProject        `json:"project"`
	Summary     string             `json:"summary"`
	Description string             `json:"description"`
	IssueType   JiraIssueType      `json:"issuetype"`
	Priority    JiraPriority       `json:"priority"`
	Components  []JiraComponent    `json:"components"`
	Customfield10103 string        `json:"customfield_10103"` // Metro Service
	Customfield10702 string        `json:"customfield_10702"` // Issue Location
	Customfield11010 string        `json:"customfield_11010"` // Metro Team
}

type JiraProject struct {
	Key string `json:"key"`
}

type JiraIssueType struct {
	Name string `json:"name"`
}

type JiraPriority struct {
	ID string `json:"id"`
}

type JiraComponent struct {
	Name string `json:"name"`
}

func main() {
	err := godotenv.Load()
	if err != nil {
		log.Fatal("Error loading .env file")
	}

	yaToken := os.Getenv("YANDEX_TOKEN")
	jiraUser := os.Getenv("JIRA_USER")
	jiraToken := os.Getenv("JIRA_TOKEN")
	jiraHost := os.Getenv("JIRA_HOST")

	client := resty.New()

	for {
		resp, err := client.R().
			SetHeader("Authorization", "OAuth "+yaToken).
			Get("https://dialogs.yandex.net/api/v1/skills/YOUR_SKILL_ID/messages")

		if err != nil {
			log.Println("Polling error:", err)
			continue
		}

		var messages []YandexMessage
		_ = json.Unmarshal(resp.Body(), &messages)

		for _, msg := range messages {
			if strings.HasPrefix(msg.Text, "/jira") {
				summary := strings.TrimPrefix(msg.Text, "/jira")
				description := "Описание указано пользователем (здесь должно быть отдельное поле)"

				issue := JiraIssue{
					Fields: JiraFields{
						Project: JiraProject{Key: "TEST"},
						Summary: summary,
						Description: description,
						IssueType: JiraIssueType{Name: "Инцидент"},
						Priority: JiraPriority{ID: "4"},
						Components: []JiraComponent{{Name: "Service Desk IT Issues"}},
						Customfield10103: "Service Management > First Line Support",
						Customfield10702: "FLS",
						Customfield11010: "Service Management. FLS",
					},
				}

				_, err := client.R().
					SetBasicAuth(jiraUser, jiraToken).
					SetHeader("Content-Type", "application/json").
					SetBody(issue).
					Post(jiraHost + "/rest/api/2/issue")

				if err != nil {
					log.Println("Jira error:", err)
				}
			}
		}

		time.Sleep(3 * time.Second)
	}
}

// Для вложений нужна отдельная функция загрузки файла
func attachToJira(issueKey, filepath, jiraHost, jiraUser, jiraToken string) error {
	file, err := os.Open(filepath)
	if err != nil {
		return err
	}
	defer file.Close()

	var b bytes.Buffer
	writer := multipart.NewWriter(&b)
	part, _ := writer.CreateFormFile("file", filepath)
	io.Copy(part, file)
	writer.Close()

	req, _ := http.NewRequest("POST", jiraHost+"/rest/api/2/issue/"+issueKey+"/attachments", &b)
	req.SetBasicAuth(jiraUser, jiraToken)
	req.Header.Set("X-Atlassian-Token", "no-check")
	req.Header.Set("Content-Type", writer.FormDataContentType())

	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {
		return err
	}
	defer resp.Body.Close()
	if resp.StatusCode >= 300 {
		return fmt.Errorf("attach failed: %v", resp.Status)
	}
	return nil
}


📂 Что нужно тебе сделать, чтобы он реально работал на проде:

Создай .env файл рядом с бинарником:

ini
Copy
Edit
YANDEX_TOKEN=ya_oauth_...
JIRA_USER=логин@domain.ru
JIRA_TOKEN=Jira_API_Token
JIRA_HOST=https://jira.example.com
Вставь реальный YOUR_SKILL_ID в URL запроса в коде:

go
Copy
Edit
Get("https://dialogs.yandex.net/api/v1/skills/YOUR_SKILL_ID/messages")
Установи зависимости:

bash
Copy
Edit
go mod init yourmodulename
go get github.com/go-resty/resty/v2
go get github.com/joho/godotenv
Собери и запусти:

bash
Copy
Edit
go run main.go
💡 Хочешь, чтобы бот вел полноценный диалог (спросил тему, потом описание, потом вложение) — я могу расширить логику с диалоговым состоянием (FSM или простая map по user_id).

📎 И, если нужно автоматически обрабатывать вложения от пользователя (фото и т.п.) — уточни, как именно ты хочешь их обрабатывать, и я это добавлю.



