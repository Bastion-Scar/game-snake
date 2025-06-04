yandex-jira-bot/
├── cmd/
│   └── main.go
├── internal/
│   ├── bot/
│   │   └── handler.go
│   ├── jira/
│   │   └── client.go
│   ├── storage/
│   │   └── offset.go
├── .env
├── go.mod
├── go.sum


.env
YANDEX_TOKEN=ваш_токен_бота
JIRA_BASE_URL=https://your-domain.atlassian.net
JIRA_USER=ваш_email@domain.com
JIRA_API_TOKEN=ваш_jira_api_token
JIRA_PROJECT_KEY=PRJ
OFFSET_FILE=offset.txt

internal/storage/offset.go
package storage

import (
	"io/ioutil"
	"os"
	"strings"
)

func LoadOffset(file string) string {
	data, err := ioutil.ReadFile(file)
	if err != nil {
		return ""
	}
	return strings.TrimSpace(string(data))
}

func SaveOffset(file, offset string) error {
	return ioutil.WriteFile(file, []byte(offset), 0644)
}


internal/jira/client.gointernal/jira/client.go

package jira

import (
	"fmt"
	"os"

	"github.com/go-resty/resty/v2"
)

type Client struct {
	BaseURL string
	User    string
	Token   string
	Project string
}

func NewClient() *Client {
	return &Client{
		BaseURL: os.Getenv("JIRA_BASE_URL"),
		User:    os.Getenv("JIRA_USER"),
		Token:   os.Getenv("JIRA_API_TOKEN"),
		Project: os.Getenv("JIRA_PROJECT_KEY"),
	}
}

func (c *Client) CreateIssue(summary string) error {
	client := resty.New()

	body := map[string]interface{}{
		"fields": map[string]interface{}{
			"project": map[string]string{
				"key": c.Project,
			},
			"summary":     summary,
			"description": "Создано ботом из Яндекс.Мессенджера",
			"issuetype": map[string]string{
				"name": "Task",
			},
		},
	}

	resp, err := client.R().
		SetBasicAuth(c.User, c.Token).
		SetHeader("Content-Type", "application/json").
		SetBody(body).
		Post(c.BaseURL + "/rest/api/3/issue")

	if err != nil {
		return err
	}
	if resp.StatusCode() >= 300 {
		return fmt.Errorf("Jira error: %s", resp.String())
	}
	return nil
}

internal/bot/handler.go


package bot

import (
	"encoding/json"
	"fmt"
	"log"
	"net/http"
	"os"
	"strings"
	"time"
	"yandex-jira-bot/internal/jira"
	"yandex-jira-bot/internal/storage"
)

type Message struct {
	ConversationMessageID string `json:"conversation_message_id"`
	Text                  string `json:"text"`
	Sender                struct {
		UserID string `json:"user_id"`
	} `json:"sender"`
}

type Update struct {
	Message Message `json:"message"`
}

func StartPolling() {
	token := os.Getenv("YANDEX_TOKEN")
	offsetFile := os.Getenv("OFFSET_FILE")
	jiraClient := jira.NewClient()

	offset := storage.LoadOffset(offsetFile)

	for {
		url := fmt.Sprintf("https://dialogs.yandex.net/api/v1/skills/%s/messages?offset=%s", token, offset)
		resp, err := http.Get(url)
		if err != nil {
			log.Println("Ошибка получения обновлений:", err)
			time.Sleep(3 * time.Second)
			continue
		}
		defer resp.Body.Close()

		var updates []Update
		if err := json.NewDecoder(resp.Body).Decode(&updates); err != nil {
			log.Println("Ошибка JSON:", err)
			continue
		}

		for _, update := range updates {
			offset = update.Message.ConversationMessageID
			storage.SaveOffset(offsetFile, offset)
			go handleMessage(update.Message, jiraClient)
		}

		time.Sleep(2 * time.Second)
	}
}

func handleMessage(msg Message, client *jira.Client) {
	text := strings.TrimSpace(msg.Text)

	if strings.HasPrefix(text, "/jira") {
		summary := strings.TrimSpace(strings.TrimPrefix(text, "/jira"))
		if summary == "" {
			log.Printf("[WARN] Пустая заявка от %s", msg.Sender.UserID)
			return
		}

		err := client.CreateIssue(summary)
		if err != nil {
			log.Printf("[ERROR] Ошибка при создании заявки от %s: %v", msg.Sender.UserID, err)
		} else {
			log.Printf("[INFO] Успешно создана заявка от %s: %s", msg.Sender.UserID, summary)
		}
	}
}


cmd/main.go

package main

import (
	"log"
	"os"

	"github.com/joho/godotenv"
	"yandex-jira-bot/internal/bot"
)

func main() {
	err := godotenv.Load()
	if err != nil {
		log.Println("Файл .env не найден. Используются переменные окружения.")
	}

	required := []string{"YANDEX_TOKEN", "JIRA_BASE_URL", "JIRA_USER", "JIRA_API_TOKEN", "JIRA_PROJECT_KEY", "OFFSET_FILE"}
	for _, key := range required {
		if os.Getenv(key) == "" {
			log.Fatalf("Переменная окружения %s обязательна", key)
		}
	}

	bot.StartPolling()
}


go.mod

module yandex-jira-bot

go 1.20

require (
	github.com/go-resty/resty/v2 v2.7.0
	github.com/joho/godotenv v1.5.1
)
